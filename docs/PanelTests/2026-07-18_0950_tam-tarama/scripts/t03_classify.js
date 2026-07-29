const { launch, record, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const candidates = ['/admin/storefront/channel-products','/admin/orders/quotes','/admin/orders/gift-cards','/admin/pos/sales','/admin/integrations/logs','/admin/finance/suppliers','/admin/fulfillment/picking-plans','/admin/settings/users','/admin/settings/channels','/admin/storefront/channel-categories'];
(async () => {
  const { browser, page } = await launch();
  await login(page);
  for (const href of candidates) {
    await page.goto(BASE + href, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const info = await page.evaluate(() => {
      const t = document.body.innerText.replace(/\s+/g, ' ');
      return { placeholder: /geliştirilme aşamasında|geliştirme aşamasında|yakında/i.test(t), excerpt: t.slice(0, 450) };
    });
    record('siniflandirma', href, info.placeholder ? 'PLACEHOLDER' : 'INFO', info.excerpt.slice(0, 350));
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
