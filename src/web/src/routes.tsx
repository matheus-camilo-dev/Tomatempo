import type { RouteObject } from 'react-router'
import { AccountScreen } from './components/AccountScreen'
import { ConfirmEmailScreen } from './components/ConfirmEmailScreen'
import { ForgotPasswordScreen } from './components/ForgotPasswordScreen'
import { HomeRoute } from './components/HomeRoute'
import { LoginScreen } from './components/LoginScreen'
import { OnboardingScreen } from './components/OnboardingScreen'
import { RegisterScreen } from './components/RegisterScreen'
import { ResetPasswordScreen } from './components/ResetPasswordScreen'

/**
 * Os caminhos em português não são estilo: `/entrar` e `/onboarding` são para
 * onde a API redireciona depois do Google, e `/confirmar-email` e
 * `/nova-senha` são os links que saem por e-mail. Trocar um deles quebra um
 * fluxo que já está no ar do outro lado.
 */
export const routes: RouteObject[] = [
  { path: '/', element: <HomeRoute /> },
  { path: '/entrar', element: <LoginScreen /> },
  { path: '/cadastrar', element: <RegisterScreen /> },
  { path: '/onboarding', element: <OnboardingScreen /> },
  { path: '/conta', element: <AccountScreen /> },
  { path: '/confirmar-email', element: <ConfirmEmailScreen /> },
  { path: '/esqueci-senha', element: <ForgotPasswordScreen /> },
  { path: '/nova-senha', element: <ResetPasswordScreen /> },
]
