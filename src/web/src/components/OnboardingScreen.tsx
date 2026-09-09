import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { useAccount, useCompleteOnboarding } from '../hooks/useAccount'
import { SIGNUP_GOALS, browserTimeZone, errorCode } from '../lib/accountForm'
import { FormAlert, GoalField, SCREEN_CLASS, SUBMIT_CLASS, TextField, TimeZoneField } from './AccountFields'

const META_PADRAO = 4

/**
 * Tela obrigatória de quem entrou pelo Google: a conta nasce sem apelido, meta
 * e fuso, e não pontua nada até escolhê-los (§3.10).
 */
export function OnboardingScreen() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { account, isLoading } = useAccount()
  const concluir = useCompleteOnboarding()

  const [apelido, setApelido] = useState('')
  const [meta, setMeta] = useState<number>(META_PADRAO)
  const [fuso, setFuso] = useState(browserTimeZone)

  if (isLoading) {
    return (
      <p role="status" className={SCREEN_CLASS}>
        {t('account.loading')}
      </p>
    )
  }

  if (!account) return <Navigate to="/entrar" replace />
  if (!account.onboardingRequired) return <Navigate to="/" replace />

  async function aoEnviar(event: FormEvent) {
    event.preventDefault()

    const feito = await concluir
      .mutateAsync({
        nickname: apelido,
        goal: meta,
        timeZone: fuso,
        locale: i18n.resolvedLanguage ?? i18n.language,
      })
      .then(() => true)
      .catch(() => false)

    if (feito) await navigate('/')
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.onboardingTitle')}
      </h1>

      <FormAlert code={errorCode(concluir.error)} />

      <form onSubmit={aoEnviar} className="flex flex-col gap-4">
        <TextField
          id="onboarding-apelido"
          label={t('account.nicknameLabel')}
          autoComplete="nickname"
          value={apelido}
          onChange={setApelido}
        />
        <GoalField id="onboarding-meta" value={meta} options={SIGNUP_GOALS} onChange={setMeta} />
        <TimeZoneField id="onboarding-fuso" value={fuso} onChange={setFuso} />

        <button type="submit" disabled={concluir.isPending} className={SUBMIT_CLASS}>
          {t('account.onboardingSubmit')}
        </button>
      </form>
    </main>
  )
}
