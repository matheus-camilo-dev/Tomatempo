import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useSignUp } from '../hooks/useAccount'
import { SIGNUP_GOALS, browserTimeZone, errorCode } from '../lib/accountForm'
import { FormAlert, GoalField, SCREEN_CLASS, SUBMIT_CLASS, TextField, TimeZoneField } from './AccountFields'

const META_PADRAO = 4

export function RegisterScreen() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const signUp = useSignUp()

  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [apelido, setApelido] = useState('')
  const [meta, setMeta] = useState<number>(META_PADRAO)
  const [fuso, setFuso] = useState(browserTimeZone)
  const [enviado, setEnviado] = useState(false)

  async function aoEnviar(event: FormEvent) {
    event.preventDefault()

    const conta = await signUp
      .mutateAsync({
        email,
        password: senha,
        nickname: apelido,
        goal: meta,
        timeZone: fuso,
        locale: i18n.resolvedLanguage ?? i18n.language,
      })
      .catch(() => undefined)

    // `undefined` é recusa da API, e o alerta já cuida. `null` é o endereço que
    // já tinha conta: o cadastro respondeu igual e não abriu sessão (§3.10) —
    // a tela não pode revelar a diferença.
    if (conta === undefined) return
    if (conta === null) {
      setEnviado(true)
      return
    }

    await navigate('/')
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.signUpTitle')}
      </h1>

      <FormAlert code={errorCode(signUp.error)} />

      {enviado && (
        <p role="status" className="text-sm text-slate-600 dark:text-slate-300">
          {t('account.checkYourEmail')}
        </p>
      )}

      <form onSubmit={aoEnviar} className="flex flex-col gap-4">
        <TextField
          id="cadastro-email"
          label={t('account.emailLabel')}
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
        />
        <TextField
          id="cadastro-senha"
          label={t('account.passwordLabel')}
          type="password"
          autoComplete="new-password"
          value={senha}
          onChange={setSenha}
        />
        <TextField
          id="cadastro-apelido"
          label={t('account.nicknameLabel')}
          autoComplete="nickname"
          value={apelido}
          onChange={setApelido}
        />
        <GoalField id="cadastro-meta" value={meta} options={SIGNUP_GOALS} onChange={setMeta} />
        <TimeZoneField id="cadastro-fuso" value={fuso} onChange={setFuso} />

        <button type="submit" disabled={signUp.isPending} className={SUBMIT_CLASS}>
          {t('account.signUpSubmit')}
        </button>
      </form>

      <p className="text-center text-sm text-slate-600 dark:text-slate-300">
        {t('account.haveAccount')}{' '}
        <Link to="/entrar" className="underline">
          {t('nav.signIn')}
        </Link>
      </p>
    </main>
  )
}
