import { setupServer } from 'msw/node'

/**
 * Servidor de mentira para os testes de front (§6.4): nada de `fetch` mockado.
 * Sobe sem rota alguma de propósito — cada teste declara o que a API responde
 * com `server.use(...)`, e requisição não declarada estoura o teste, que é
 * como o critério "visitante anônimo não chama a API" se verifica.
 */
export const server = setupServer()
