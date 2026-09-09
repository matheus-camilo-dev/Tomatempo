import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useForgotPassword } from '../hooks/useAccount'
import { errorCode } from '../lib/accountForm'
import { FormAlert, SCREEN_CLASS, SUBMIT_CLASS, TextField } from './AccountFields'

/**
 * A API responde 202 com ou sem conta no endereço (§3.10). A tela responde
 * igual nos dois casos — mostrar "não encontramos essa conta" desfaria no
 * front exatamente o que a API protege.
 */
export function ForgotPasswordScreen() {
  const { t } = useTranslation()
  const pedir = useForgotPassword()
  const [email, setEmail] = useState('')

  function aoEnviar(event: FormEvent) {
    event.preventDefault()
    pedir.mutate(email)
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.forgotTitle')}
      </h1>

      <FormAlert code={errorCode(pedir.error)} />

      {pedir.isSuccess ? (
        <p role="status" className="text-slate-600 dark:text-slate-300">
          {t('account.forgotRequested')}
        </p>
      ) : (
        <form onSubmit={aoEnviar} className="flex flex-col gap-4">
          <TextField
            id="recuperar-email"
            label={t('account.emailLabel')}
            type="email"
            autoComplete="email"
            value={email}
            onChange={setEmail}
          />
          <button type="submit" disabled={pedir.isPending} className={SUBMIT_CLASS}>
            {t('account.forgotSubmit')}
          </button>
        </form>
      )}

      <p className="text-center text-sm text-slate-600 dark:text-slate-300">
        <Link to="/entrar" className="underline">
          {t('nav.signIn')}
        </Link>
      </p>
    </main>
  )
}
