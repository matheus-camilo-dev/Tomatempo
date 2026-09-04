"""Gate de cobertura do domínio (docs/ESPECIFICACAO-TECNICA.md §6.6).

Lê o cobertura.xml gerado pelo coletor do coverlet e falha quando linha ou branch ficam
abaixo do mínimo. Existe como script, e não como flag do runner, porque o gate precisa dizer
*qual* dos dois números reprovou — "cobertura baixa" sem o número não ajuda ninguém a decidir
se o caso descoberto é requisito ou branch que não deveria existir (§6.8).

Uso: gate_cobertura.py <diretorio-com-o-relatorio> <minimo-em-porcentagem>
"""

import glob
import os
import sys
import xml.etree.ElementTree as ET

diretorio, minimo = sys.argv[1], float(sys.argv[2])

relatorios = glob.glob(os.path.join(diretorio, "**", "coverage.cobertura.xml"), recursive=True)
if not relatorios:
    sys.exit(f"nenhum coverage.cobertura.xml encontrado em {diretorio}")

raiz = ET.parse(relatorios[0]).getroot()
linha = float(raiz.get("line-rate")) * 100
branch = float(raiz.get("branch-rate")) * 100

print(f"cobertura de linha:  {linha:.1f}% (mínimo {minimo:.0f}%)")
print(f"cobertura de branch: {branch:.1f}% (mínimo {minimo:.0f}%)")

reprovados = [nome for nome, valor in (("linha", linha), ("branch", branch)) if valor < minimo]
if reprovados:
    sys.exit(f"cobertura abaixo do mínimo em: {', '.join(reprovados)}")
