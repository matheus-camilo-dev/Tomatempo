import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { RouterProvider, createMemoryRouter } from 'react-router'
import { createQueryClient } from '../lib/queryClient'
import { routes } from '../routes'

export interface RenderRouteOptions {
  /** Caminho inicial, como o navegador entregaria — inclusive com query. */
  readonly path?: string
}

/**
 * Monta o app inteiro numa rota, com roteador de memória e um QueryClient
 * novo por teste — cache compartilhado entre casos faria um teste enxergar a
 * conta que o anterior carregou.
 */
export function renderRoute({ path = '/' }: RenderRouteOptions = {}) {
  const queryClient = createQueryClient()
  const router = createMemoryRouter(routes, { initialEntries: [path] })

  const result = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return { ...result, router, queryClient }
}
