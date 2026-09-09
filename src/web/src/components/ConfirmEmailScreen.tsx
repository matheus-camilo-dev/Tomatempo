import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { useConfirmEmail } from '../hooks/useAccount'
import { errorCode } from '../lib/accountForm'
import { FormAlert, SCREEN_CLASS } from './AccountFields'

export function ConfirmEmailScreen() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const confirmar = useConfirmEmail()

  const token = params.get('token')
  // O token serve uma vez só (§3.10): sem a trava, o segundo disparo do efeito
  // em modo estrito queimaria o token e a tela mostraria "link inválido" para
  // quem acabou de usar um link válido.
  const disparado = useRef(false)

  useEffect(() => {
    if (disparado.current || token === null) return

    disparado.current = true
    confirmar.mutate(token)
  }, [confirmar, token])

  const codigo = token === null ? 'invalid_token' : errorCode(confirmar.error)

  return (
    <main className={SCREEN_CLASS}>
      <FormAlert code={codigo} />

      {codigo === null &&
        (confirmar.isSuccess ? (
          <p role="status" className="text-slate-900 dark:text-slate-50">
            {t('account.emailConfirmed')}
          </p>
        ) : (
          <p role="status" className="text-slate-600 dark:text-slate-300">
            {t('account.confirmingEmail')}
          </p>
        ))}

      <p className="text-center text-sm">
        <Link to="/" className="underline">
          {t('app.name')}
        </Link>
      </p>
    </main>
  )
}
