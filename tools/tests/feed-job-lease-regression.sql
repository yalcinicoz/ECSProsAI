-- FAZ 11 / K0 — integration.feed_jobs lease regresyonu.
-- Önkoşul: 20260830190000_AddFeedJobLeases migration uygulanmış test DB'si.
-- Çalıştırma örneği (secret'i komut satırına yazmayın):
--   psql "$TEST_CONNECTION_STRING" -v ON_ERROR_STOP=1 -f tools/tests/feed-job-lease-regression.sql
-- Tüm değişiklikler transaction sonunda ROLLBACK edilir.

BEGIN;

DO $$
DECLARE
    test_platform uuid := gen_random_uuid();
    test_job uuid := gen_random_uuid();
    exhausted_job uuid := gen_random_uuid();
    claimed_job uuid;
    second_claim uuid;
    reclaimed_job uuid;
BEGIN
    INSERT INTO integration.feed_jobs
        ("Id", "FirmPlatformId", "RequestedAt", "Status", "AttemptCount", "CreatedAt", "IsDeleted")
    VALUES
        (test_job, test_platform, NOW(), 'pending', 0, NOW(), false);

    WITH candidate AS (
        SELECT "Id" FROM integration.feed_jobs
        WHERE "Id" = test_job AND "Status" = 'pending' AND "RequestedAt" <= NOW() AND "IsDeleted" = false
        ORDER BY "RequestedAt", "CreatedAt" LIMIT 1 FOR UPDATE SKIP LOCKED
    )
    UPDATE integration.feed_jobs AS jobs
    SET "Status" = 'processing', "LeaseOwner" = 'lease-test-1',
        "LeaseUntil" = NOW() + INTERVAL '15 minutes',
        "AttemptCount" = jobs."AttemptCount" + 1, "StartedAt" = NOW(), "UpdatedAt" = NOW()
    FROM candidate
    WHERE jobs."Id" = candidate."Id" AND jobs."Id" = test_job
    RETURNING jobs."Id" INTO claimed_job;

    IF claimed_job IS DISTINCT FROM test_job THEN
        RAISE EXCEPTION 'İlk claim başarısız';
    END IF;

    SELECT "Id" INTO second_claim
    FROM integration.feed_jobs
    WHERE "Id" = test_job
      AND ("Status" = 'pending' OR ("Status" = 'processing' AND "LeaseUntil" <= NOW()));
    IF second_claim IS NOT NULL THEN
        RAISE EXCEPTION 'Aktif lease varken iş ikinci kez alınabilir durumda';
    END IF;

    UPDATE integration.feed_jobs SET "LeaseUntil" = NOW() - INTERVAL '1 second' WHERE "Id" = test_job;

    WITH candidate AS (
        SELECT "Id" FROM integration.feed_jobs
        WHERE "Id" = test_job AND "Status" = 'processing' AND "LeaseUntil" <= NOW()
        FOR UPDATE SKIP LOCKED
    )
    UPDATE integration.feed_jobs AS jobs
    SET "LeaseOwner" = 'lease-test-2', "LeaseUntil" = NOW() + INTERVAL '15 minutes',
        "AttemptCount" = jobs."AttemptCount" + 1, "UpdatedAt" = NOW()
    FROM candidate
    WHERE jobs."Id" = candidate."Id"
    RETURNING jobs."Id" INTO reclaimed_job;

    IF reclaimed_job IS DISTINCT FROM test_job THEN
        RAISE EXCEPTION 'Süresi dolan lease devralınamadı';
    END IF;

    UPDATE integration.feed_jobs
    SET "Status" = 'completed', "CompletedAt" = NOW(), "LeaseOwner" = NULL, "LeaseUntil" = NULL
    WHERE "Id" = test_job AND "LeaseOwner" = 'lease-test-2';

    IF EXISTS (
        SELECT 1 FROM integration.feed_jobs
        WHERE "Id" = test_job AND "Status" IN ('pending', 'processing')
    ) THEN
        RAISE EXCEPTION 'Tamamlanan iş yeniden claim edilebilir durumda';
    END IF;

    INSERT INTO integration.feed_jobs
        ("Id", "FirmPlatformId", "RequestedAt", "Status", "AttemptCount", "LeaseOwner",
         "LeaseUntil", "CreatedAt", "UpdatedAt", "IsDeleted")
    VALUES
        (exhausted_job, test_platform, NOW(), 'processing', 5, 'dead-node',
         NOW() - INTERVAL '1 second', NOW(), NOW(), false);

    UPDATE integration.feed_jobs
    SET "Status" = 'failed', "CompletedAt" = NOW(), "LeaseOwner" = NULL, "LeaseUntil" = NULL,
        "LastError" = COALESCE("LastError", 'Worker kaybı sonrası maksimum deneme sayısına ulaşıldı.'),
        "UpdatedAt" = NOW()
    WHERE "Id" = exhausted_job AND "Status" = 'processing'
      AND "LeaseUntil" <= NOW() AND "AttemptCount" >= 5;

    IF NOT EXISTS (
        SELECT 1 FROM integration.feed_jobs
        WHERE "Id" = exhausted_job AND "Status" = 'failed'
          AND "LeaseOwner" IS NULL AND "LeaseUntil" IS NULL
    ) THEN
        RAISE EXCEPTION 'Retry limiti dolan expired lease failed durumuna geçirilmedi';
    END IF;
END $$;

ROLLBACK;

\echo 'feed-job-lease-regression: OK (transaction rollback edildi)'
