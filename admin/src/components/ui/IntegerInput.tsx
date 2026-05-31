/**
 * IntegerInput — tam sayı girişi için standart bileşen.
 * - Spinner yok, ondalık yok
 * - Sadece rakam ve eksi kabul eder
 * - Yazarken binler ayracı eklenir (1.234)
 */
import { NumericInput, type NumericInputProps } from './NumericInput'

export type IntegerInputProps = Omit<NumericInputProps, 'decimals'>

export function IntegerInput(props: IntegerInputProps) {
  return <NumericInput decimals={0} {...props} />
}
