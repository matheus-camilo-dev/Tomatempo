import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router'
import { useResetPassword } from '../hooks/useAccount'
import { errorCode } from '../lib/accountForm'
import { FormAlert, SCREEN_CLASS, SUBMIT_CLASS, TextField } from './AccountFields'

export function ResetPasswordScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const trocar = useResetPassword()

  const token = params.get('token')
  const [senha, setSenha] = useState('')

  async function aoEnviar(event: FormEvent) {
    event.preventDefault()
    if (token === null) return

    const feito = await trocar
      .mutateAsync({ token, password: senha })
      .then(() => true)
      .catch(() => false)

    // A troca derruba as sessões anteriores (§3.10): daqui a pessoa só volta
    // entrando com a senha nova.
    if (feito) await navigate('/entrar')
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.resetTitle')}
      </h1>

      <FormAlert code={token === null ? 'invalid_token' : errorCode(trocar.error)} />

      {token !== null && (
        <form onSubmit={aoEnviar} className="flex flex-col gap-4">
          <TextField
            id="nova-senha"
            label={t('account.newPasswordLabel')}
            type="password"
            autoComplete="new-password"
            value={senha}
            onChange={setSenha}
          />
          <button type="submit" disabled={trocar.isPending} className={SUBMIT_CLASS}>
            {t('account.resetSubmit')}
          </button>
        </form>
      )}
    </main>
  )
}
