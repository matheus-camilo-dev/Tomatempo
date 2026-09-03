/** Storage isolado por teste — nada vaza entre casos nem para o localStorage real. */
export function memoryStorage(seed: Record<string, string> = {}): Storage {
  const data = new Map(Object.entries(seed))

  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key: string) => data.get(key) ?? null,
    key: (index: number) => [...data.keys()][index] ?? null,
    removeItem: (key: string) => void data.delete(key),
    setItem: (key: string, value: string) => void data.set(key, value),
  }
}
