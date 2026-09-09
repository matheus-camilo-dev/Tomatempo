import { QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { RouterProvider, createBrowserRouter } from 'react-router'
import { createQueryClient } from './lib/queryClient'
import { routes } from './routes'

const router = createBrowserRouter(routes)

export default function App() {
  // Um cliente por montagem, criado uma vez: recriar a cada render jogaria
  // fora o cache e refaria `GET /me` sem parar.
  const [queryClient] = useState(createQueryClient)

  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
