import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { API_BASE } from '../lib/api'
import { useSignIn } from '../hooks/useAccount'
import { errorCode } from '../lib/accountForm'
import { FormAlert, SCREEN_CLASS, SUBMIT_CLASS, TextField } from './AccountFields'

export function LoginScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const signIn = useSignIn()

  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')

  // A volta do Google não passa pelo cliente: o servidor redireciona para cá
  // com o motivo na URL, e é a única pista que a pessoa recebe (§3.10).
  const codigo = errorCode(signIn.error) ?? params.get('erro')

  async function aoEnviar(event: FormEvent) {
    event.preventDefault()

    const conta = await signIn.mutateAsync({ email, password: senha }).catch(() => undefined)
    if (!conta) return

    await navigate('/')
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.signInTitle')}
      </h1>

      <FormAlert code={codigo} />

      <form onSubmit={aoEnviar} className="flex flex-col gap-4">
        <TextField
          id="login-email"
          label={t('account.emailLabel')}
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
        />
        <TextField
          id="login-senha"
          label={t('account.passwordLabel')}
          type="password"
          autoComplete="current-password"
          value={senha}
          onChange={setSenha}
        />

        <button type="submit" disabled={signIn.isPending} className={SUBMIT_CLASS}>
          {t('account.signInSubmit')}
        </button>
      </form>

      {/*
        Âncora de verdade, e não navegação do roteador: `/api/auth/google` é
        endpoint do servidor, que responde com o desafio do provedor. Passar
        pelo React Router aqui só levaria a uma rota que não existe.
      */}
      <a
        href={`${API_BASE}/auth/google`}
        className="rounded-full border border-slate-300 px-8 py-3 text-center text-base font-medium text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
      >
        {t('account.signInWithGoogle')}
      </a>

      <p className="text-center text-sm text-slate-600 dark:text-slate-300">
        <Link to="/esqueci-senha" className="underline">
          {t('account.forgotLink')}
        </Link>
      </p>

      <p className="text-center text-sm text-slate-600 dark:text-slate-300">
        {t('account.noAccount')}{' '}
        <Link to="/cadastrar" className="underline">
          {t('nav.signUp')}
        </Link>
      </p>
    </main>
  )
}
