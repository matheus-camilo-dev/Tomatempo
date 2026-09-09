import { useTranslation } from 'react-i18next'
import { Link, Navigate } from 'react-router'
import { useAccount, useResendConfirmation } from '../hooks/useAccount'
import { TimerScreen } from './TimerScreen'

/**
 * Tela inicial. É a mesma para quem entrou e para quem nunca entrou: o timer
 * é público (§3.9), e a conta só acrescenta a barra de cima.
 */
export function HomeRoute() {
  const { t } = useTranslation()
  const { account } = useAccount()

  // Conta que não terminou de nascer não usa o app: a API responde 409 nos
  // endpoints de sessão até o onboarding sair (§3.10).
  if (account?.onboardingRequired) return <Navigate to="/onboarding" replace />

  return (
    <>
      <nav className="mx-auto flex max-w-md justify-end px-6 pt-4 text-sm">
        {account ? (
          <Link to="/conta" className="text-slate-600 underline dark:text-slate-300">
            {t('nav.account')}
          </Link>
        ) : (
          <Link to="/entrar" className="text-slate-600 underline dark:text-slate-300">
            {t('nav.signIn')}
          </Link>
        )}
      </nav>

      {account && !account.emailConfirmed && <UnconfirmedEmail email={account.email} />}

      {/* O CTA sai daqui e não do timer: dentro da rota ele navega sem recarregar. */}
      <TimerScreen signupCta={false} />

      {!account && (
        <p
          data-testid="signup-cta"
          className="mx-auto max-w-xs px-6 pb-12 text-center text-sm leading-relaxed text-slate-500 dark:text-slate-400"
        >
          {t('cta')}{' '}
          <Link to="/cadastrar" className="underline">
            {t('nav.signUp')}
          </Link>
        </p>
      )}
    </>
  )
}

function UnconfirmedEmail({ email }: { readonly email: string | null }) {
  const { t } = useTranslation()
  const resend = useResendConfirmation()

  return (
    <div className="mx-auto flex max-w-md flex-col items-start gap-2 px-6 pt-4 text-sm">
      <p className="rounded-lg bg-amber-100 px-4 py-2 text-amber-900 dark:bg-amber-900/40 dark:text-amber-100">
        {t('account.unconfirmedEmail')}
      </p>

      {resend.isSuccess ? (
        <p role="status" className="text-slate-600 dark:text-slate-300">
          {t('account.resendRequested')}
        </p>
      ) : (
        <button
          type="button"
          disabled={email === null || resend.isPending}
          onClick={() => email !== null && resend.mutate(email)}
          className="underline disabled:opacity-60"
        >
          {t('account.resendConfirmation')}
        </button>
      )}
    </div>
  )
}
