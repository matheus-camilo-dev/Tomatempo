import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import {
  useAccount,
  useDeleteAccount,
  useFreezes,
  useSignOut,
  useUpdateGoal,
} from '../hooks/useAccount'
import { GOAL_RANGE, errorCode } from '../lib/accountForm'
import type { Account, FreezeEntry, GoalChange } from '../lib/contracts'
import { formatIsoDate } from '../lib/formatDate'
import { FormAlert, GoalField, SCREEN_CLASS, SUBMIT_CLASS, TextField } from './AccountFields'

/** Motivos que o ledger devolve (§2, `freeze_ledger`). Motivo fora da lista não vira texto cru na tela. */
const FREEZE_REASONS = ['Signup', 'StreakMilestone', 'WeeklyRanking', 'DayCovered', 'CapOverflow']

export function AccountScreen() {
  const { t } = useTranslation()
  const { account, isLoading } = useAccount()

  if (isLoading) {
    return (
      <p role="status" className={SCREEN_CLASS}>
        {t('account.loading')}
      </p>
    )
  }

  if (!account) return <Navigate to="/entrar" replace />
  if (account.onboardingRequired) return <Navigate to="/onboarding" replace />

  return <AccountDetails account={account} />
}

function AccountDetails({ account }: { readonly account: Account }) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { signOut } = useSignOut()
  const freezes = useFreezes(true)
  const atualizarMeta = useUpdateGoal()

  const [meta, setMeta] = useState(account.goal?.current ?? 1)
  const [futura, setFutura] = useState<GoalChange | null>(null)

  const upcoming =
    futura ??
    (account.goal?.upcoming !== null && account.goal?.upcomingEffectiveFrom
      ? { pomodoros: account.goal.upcoming, effectiveFrom: account.goal.upcomingEffectiveFrom }
      : null)

  const idioma = i18n.resolvedLanguage ?? i18n.language

  async function salvarMeta(event: FormEvent) {
    event.preventDefault()

    const mudanca = await atualizarMeta.mutateAsync(meta).catch(() => null)
    if (mudanca) setFutura(mudanca)
  }

  async function sair() {
    await signOut()
    await navigate('/')
  }

  return (
    <main className={SCREEN_CLASS}>
      <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-50">
        {t('account.title')}
      </h1>

      <p className="text-sm text-slate-600 dark:text-slate-300">
        {account.nickname} · {account.email}
      </p>

      <dl className="grid grid-cols-3 gap-4 text-center">
        <div>
          <dt className="text-xs text-slate-500 dark:text-slate-400">
            {t('account.goalCurrentLabel')}
          </dt>
          <dd data-testid="goal-current" className="text-2xl font-semibold tabular-nums">
            {account.goal?.current ?? 0}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500 dark:text-slate-400">{t('account.streakLabel')}</dt>
          <dd data-testid="streak-current" className="text-2xl font-semibold tabular-nums">
            {account.streak.current}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-slate-500 dark:text-slate-400">{t('account.freezesLabel')}</dt>
          {/*
            O saldo sai do ledger, e só dele: o número em `/me` é a mesma conta
            feita antes, e mostrar um enquanto o outro carrega faria a tela
            piscar dois valores diferentes para o mesmo dado (§5).
          */}
          {freezes.data && (
            <dd data-testid="freezes-balance" className="text-2xl font-semibold tabular-nums">
              {freezes.data.balance}
            </dd>
          )}
        </div>
      </dl>

      {upcoming && (
        <p data-testid="goal-upcoming" className="text-sm text-slate-600 dark:text-slate-300">
          {t('account.goalUpcoming', {
            pomodoros: upcoming.pomodoros,
            date: formatIsoDate(upcoming.effectiveFrom, idioma),
          })}
        </p>
      )}

      <FormAlert code={errorCode(atualizarMeta.error)} />

      <form onSubmit={salvarMeta} className="flex flex-col gap-4">
        <GoalField id="conta-meta" value={meta} options={GOAL_RANGE} onChange={setMeta} />
        <button type="submit" disabled={atualizarMeta.isPending} className={SUBMIT_CLASS}>
          {t('account.saveGoal')}
        </button>
      </form>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-medium text-slate-900 dark:text-slate-50">
          {t('account.freezesTitle')}
        </h2>
        <ul data-testid="freezes-ledger" className="flex flex-col gap-1 text-sm">
          {(freezes.data?.entries ?? []).map((entry) => (
            <FreezeLine key={`${entry.createdAt}-${entry.reason}`} entry={entry} locale={idioma} />
          ))}
        </ul>
      </section>

      <button type="button" onClick={sair} className={SUBMIT_CLASS}>
        {t('account.signOut')}
      </button>

      <DeleteAccount account={account} />
    </main>
  )
}

function FreezeLine({
  entry,
  locale,
}: {
  readonly entry: FreezeEntry
  readonly locale: string
}) {
  const { t } = useTranslation()
  const motivo = FREEZE_REASONS.includes(entry.reason) ? entry.reason : 'unknown'

  return (
    <li className="flex justify-between gap-4 text-slate-600 dark:text-slate-300">
      <span>{t(`freeze.reason.${motivo}`)}</span>
      <span className="tabular-nums">
        {entry.delta > 0 ? `+${entry.delta}` : entry.delta}
        {entry.refDate ? ` · ${formatIsoDate(entry.refDate, locale)}` : ''}
      </span>
    </li>
  )
}

/**
 * Exclusão (§3.10). Conta com senha precisa digitá-la: sessão esquecida aberta,
 * ou roubada, não apaga a conta de ninguém. Conta criada pelo Google não tem
 * senha para digitar, e sai só com a sessão.
 *
 * Recebe a conta inteira, e não o booleano: uma prop terminada em `Password`
 * casa com a regra de connection string do gitleaks, e afrouxar a regra para
 * caber uma conveniência de escrita seria pagar em segurança (§7.5).
 */
function DeleteAccount({ account }: { readonly account: Account }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const excluir = useDeleteAccount()
  const [senha, setSenha] = useState('')

  async function aoEnviar(event: FormEvent) {
    event.preventDefault()

    const feito = await excluir
      .mutateAsync(account.hasPassword ? senha : null)
      .then(() => true)
      .catch(() => false)

    if (feito) await navigate('/')
  }

  return (
    <section className="flex flex-col gap-4 border-t border-slate-200 pt-6 dark:border-slate-700">
      <h2 className="text-lg font-medium text-slate-900 dark:text-slate-50">
        {t('account.deleteTitle')}
      </h2>

      <FormAlert code={errorCode(excluir.error)} />

      <form onSubmit={aoEnviar} className="flex flex-col gap-4">
        {account.hasPassword && (
          <TextField
            id="conta-exclusao-senha"
            label={t('account.deletePasswordLabel')}
            type="password"
            autoComplete="current-password"
            value={senha}
            onChange={setSenha}
          />
        )}

        <button
          type="submit"
          disabled={excluir.isPending}
          className="w-full rounded-full border border-red-300 px-8 py-3 text-base font-medium text-red-700 transition hover:bg-red-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-700 disabled:opacity-60 dark:border-red-700 dark:text-red-300 dark:hover:bg-red-950"
        >
          {t('account.deleteSubmit')}
        </button>
      </form>
    </section>
  )
}
