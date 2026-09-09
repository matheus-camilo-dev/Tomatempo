import { type ReactNode, memo } from 'react'
import { useTranslation } from 'react-i18next'
import { problemMessageKey } from '../lib/problemMessage'

/**
 * Peças repetidas pelas telas de conta. Nada aqui decide regra — só rotula,
 * traduz e alinha.
 */

const INPUT_CLASS =
  'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-50'

export const SUBMIT_CLASS =
  'w-full rounded-full bg-slate-900 px-8 py-3 text-base font-medium text-white transition hover:bg-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 disabled:opacity-60 dark:bg-slate-50 dark:text-slate-900 dark:hover:bg-slate-200'

export const SCREEN_CLASS =
  'mx-auto flex min-h-screen max-w-md flex-col justify-center gap-6 px-6 py-12'

export interface FieldProps {
  readonly id: string
  readonly label: string
  readonly children: ReactNode
}

export function Field({ id, label, children }: FieldProps) {
  return (
    <div className="flex flex-col gap-1 text-left">
      <label htmlFor={id} className="text-sm text-slate-600 dark:text-slate-300">
        {label}
      </label>
      {children}
    </div>
  )
}

export interface TextFieldProps {
  readonly id: string
  readonly label: string
  readonly type?: 'text' | 'email' | 'password'
  readonly value: string
  readonly autoComplete?: string
  readonly required?: boolean
  onChange(value: string): void
}

export function TextField({
  id,
  label,
  type = 'text',
  value,
  autoComplete,
  required = true,
  onChange,
}: TextFieldProps) {
  return (
    <Field id={id} label={label}>
      <input
        id={id}
        type={type}
        value={value}
        required={required}
        autoComplete={autoComplete}
        onChange={(event) => onChange(event.target.value)}
        className={INPUT_CLASS}
      />
    </Field>
  )
}

export interface GoalFieldProps {
  readonly id: string
  readonly value: number
  readonly options: readonly number[]
  onChange(value: number): void
}

export function GoalField({ id, value, options, onChange }: GoalFieldProps) {
  const { t } = useTranslation()

  return (
    <Field id={id} label={t('account.goalLabel')}>
      <select
        id={id}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className={INPUT_CLASS}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </Field>
  )
}

const TIME_ZONE_LIST_ID = 'fusos-horarios'

/** Umas seiscentas entradas, iguais durante toda a vida da aba: montadas uma vez só. */
const TIME_ZONES: readonly string[] = Intl.supportedValuesOf('timeZone')

export interface TimeZoneFieldProps {
  readonly id: string
  readonly value: string
  onChange(value: string): void
}

/**
 * Campo livre com sugestões, e não menu fechado: a lista IANA muda com o
 * tempo, e quem já sabe o próprio fuso digita mais rápido do que rola. Quem
 * valida de verdade é a API, que responde `invalid_time_zone`.
 *
 * `memo` não é otimização de gosto: sem ele, cada tecla digitada em qualquer
 * outro campo do formulário repinta as seiscentas opções da lista.
 */
export const TimeZoneField = memo(function TimeZoneField({
  id,
  value,
  onChange,
}: TimeZoneFieldProps) {
  const { t } = useTranslation()

  return (
    <Field id={id} label={t('account.timeZoneLabel')}>
      <input
        id={id}
        type="text"
        list={TIME_ZONE_LIST_ID}
        value={value}
        required
        onChange={(event) => onChange(event.target.value)}
        className={INPUT_CLASS}
      />
      <datalist id={TIME_ZONE_LIST_ID}>
        {TIME_ZONES.map((zone) => (
          <option key={zone} value={zone} />
        ))}
      </datalist>
    </Field>
  )
})

export interface FormAlertProps {
  readonly code: string | null
}

/**
 * Mensagem de erro vinda do catálogo, pela chave do `code`. O texto que o
 * servidor mandou nunca chega aqui (§4).
 */
export function FormAlert({ code }: FormAlertProps) {
  const { t } = useTranslation()

  if (code === null) return null

  return (
    <p
      role="alert"
      className="rounded-lg bg-red-100 px-4 py-2 text-sm text-red-900 dark:bg-red-900/40 dark:text-red-100"
    >
      {t(problemMessageKey(code))}
    </p>
  )
}
