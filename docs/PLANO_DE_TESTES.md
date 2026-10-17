# Plano de testes com o TriAxis real

Objetivo: provar, na ordem, que (1) a captura não corrompe o dump, (2) o arquivo é salvo de forma confiável e (3) a restauração é fiel e segura. Só depois disso libera-se a edição.

**Preparação**
- Use o `.exe` publicado e uma interface MIDI USB de boa qualidade.
- Ligue o TriAxis **direto** na interface, sem FCB1010 nem G-Major na cadeia.
- Anote o modelo da interface, a versão do driver, a versão do Windows e, se visível, a do firmware do TriAxis.
- Antes de qualquer restauração, garanta **pelo menos 2 backups idênticos** salvos em dois lugares (PC e pendrive).
- Marque cada item como ✅ / ❌ e guarde o log (`%LOCALAPPDATA%\TriAxisBackupEditor\logs`).

## A. Conexão (nada é enviado)

| # | Passo | Resultado esperado |
|---|---|---|
| A1 | Abrir o programa sem a interface conectada | Abre normalmente; aviso "Nenhuma interface MIDI encontrada"; Biblioteca e Visualizar funcionam |
| A2 | Conectar a interface com o programa aberto | Aparece na lista em até ~3 s, sem clicar em nada |
| A3 | Selecionar IN/OUT, fechar e reabrir o programa | A seleção é lembrada |
| A4 | **Testar conexão** e mudar de preset no TriAxis durante a escuta | ✔ IN, ✔ OUT. Se o TriAxis transmite Program Change, as mensagens recebidas aparecem |
| A5 | Desconectar o USB com as portas selecionadas | Sem travar; aviso de desconexão; a seleção é limpa |
| A6 | Abrir a mesma interface num DAW e testar | Mensagem clara "porta em uso por outro programa" |

## B. Backup: captura fiel

| # | Passo | Resultado esperado |
|---|---|---|
| B1 | **Fazer backup** → SHIFT + PRESET DOWN | Barra avança; termina em "Backup salvo"; arquivo `TriAxis_Backup_...syx` |
| B2 | Anotar tamanho e cabeçalho | **Esperado: 1.126 bytes, `F0 00 00 4A 02`** (confirmado num TriAxis real em 2026-10-16). Se diferente, anote: é a informação mais importante deste plano |
| B3 | Repetir B1 duas vezes sem tocar no TriAxis | **3 hashes SHA-256 idênticos** (Biblioteca → Comparar → IDÊNTICOS) |
| B4 | Repetir B1 com intervalos e cabos diferentes (se tiver) | Mesmo hash |
| B5 | Fazer backup e **não** pressionar nada | Após 60 s, "Nenhum dado recebido", sem arquivo na biblioteca |
| B6 | Iniciar backup e clicar **Cancelar** antes do dump | "Backup cancelado", nada salvo |
| B7 | Puxar o cabo MIDI no meio do dump | Após 10 s, "BACKUP INCOMPLETO"; arquivo só em `Incompletos\*.syx.part`; não aparece na biblioteca |
| B8 | Pasta de destino sem permissão (ex.: `C:\Windows`) | Mensagem clara; com a captura completa, o dump é salvo na pasta alternativa |
| B9 | Abrir o backup em **Visualizar dump** | Hex + ASCII; com 1.126 bytes, tabela de 90 presets. Um arquivo de 2.746 bytes do editor web também abre (aviso de que só os primeiros 1.126 bytes contam) |
| B10 | Conferir 3 presets na tabela contra o painel do TriAxis | Valores batem? Anote divergências (validação do mapa) |

## C. Restauração: segurança (bloqueios)

| # | Passo | Resultado esperado |
|---|---|---|
| C1 | Selecionar um arquivo de 0 bytes | "Arquivo inválido", botão Restaurar desabilitado |
| C2 | Arquivo sem `F7` final (copie um backup e corte o último byte num editor hex) | Bloqueado: "não termina com F7" |
| C3 | Arquivo sem `F0` inicial | Bloqueado |
| C4 | Clicar Restaurar → Cancelar na janela vermelha | "Nada foi enviado"; o display do TriAxis **não** pisca |
| C5 | Marcar só a etapa 1 | Botão "Enviar" continua desabilitado |
| C6 | Digitar `restaurar` (minúsculas) | Continua desabilitado |
| C7 | Arquivo de outro equipamento (`.syx` de outro fabricante) | Avisos de cabeçalho/tamanho; exige a caixa "Li os avisos" |
| C8 | Tentar fechar o programa durante o envio | O fechamento é recusado com aviso |

## D. Restauração: fidelidade (o teste mais importante)

| # | Passo | Resultado esperado |
|---|---|---|
| D1 | Backup **A** (estado original) | Salvo, hash H(A) |
| D2 | Alterar 2–3 presets à mão no TriAxis e salvá-los | — |
| D3 | Backup **B** | H(B) ≠ H(A); a comparação mostra diferenças nos presets alterados |
| D4 | Restaurar **A** (128 bytes / 20 ms) usando o botão de backup de segurança da janela | Backup de segurança salvo; envio concluído; **display do TriAxis pisca** |
| D5 | Conferir no painel os presets alterados | Voltaram aos valores originais |
| D6 | Backup **C** | **H(C) = H(A)**: ida e volta bit a bit |
| D7 | Repetir D4–D6 com 5 ms e com 50 ms de intervalo | Em 50 ms deve sempre funcionar. Anote se 5 ms falha com a sua interface |
| D8 | Repetir D4 com bloco de 4096 (mensagem inteira de uma vez) | Anote se funciona: indica se a interface aguenta SysEx sem pausas |

## E. Biblioteca

| # | Passo | Resultado esperado |
|---|---|---|
| E1 | Buscar por parte do nome, por `16/10/2026` e por palavra da observação | Filtra corretamente |
| E2 | Duplicar, renomear (com acentos), editar a observação | Arquivo e biblioteca atualizados; nunca sobrescreve |
| E3 | Excluir → "Sim" | Arquivo vai para a Lixeira (recuperável) e sai da biblioteca |
| E4 | Exportar CSV e abrir no Excel | Colunas corretas, acentos OK |
| E5 | Importar um `.syx` de fora da pasta | Copiado para a pasta de backups e registrado |

## F. Editor visual (somente depois de D6 passar)

| # | Passo | Resultado esperado |
|---|---|---|
| F1 | Editor → Abrir último backup → mudar só o **Gain** do preset 1 | Lista de alterações mostra exatamente `Preset 01 · Gain: x → y` |
| F2 | Salvar como novo .syx | Arquivo `TriAxis_Editado_...` na biblioteca; o original tem o mesmo hash de antes |
| F3 | Enviar ao TriAxis (fluxo de restauração) | Exige confirmação + aviso de mapa preliminar; display pisca |
| F4 | Conferir o preset 1 no painel do TriAxis | O Gain mostra o novo valor e os outros parâmetros estão iguais. **Este é o teste que valida o mapa do Gain** |
| F5 | Repetir F1–F4 para cada parâmetro, um modo de LED e um switch | Cada um bate com o painel. Se algum divergir, anote e não use o editor para ele |
| F6 | Backup após F3 e comparar com o arquivo editado | **Hashes idênticos** |
| F7 | Restaurar o backup original | Tudo volta ao estado inicial |

## G. Modo controle (seguro: só Program Change/CC)

| # | Passo | Resultado esperado |
|---|---|---|
| G1 | Conferir o canal MIDI do TriAxis e usar o mesmo na aba Controle → Ativar | Status verde; Backup/Restaurar ficam desabilitados |
| G2 | Clicar nos pedais 1–10 do banco 0 | O TriAxis muda para os programas 1–10; o LED do pedal acende |
| G3 | Banco ▲ e pedal 1 | Programa 11 |
| G4 | Teclas 1–0, ↑ ↓, ← → | Mesmo comportamento dos botões |
| G5 | Canal errado (ex.: 2 com o TriAxis no 1) | O TriAxis não reage (confirma o filtro de canal) |
| G6 | Com a FCB1010 ligada ao PC: Aprender "Pedal 1" e pisar no pedal | O gatilho aparece; pisar de novo envia o programa |
| G7 | Desconectar a interface com o modo ativo e clicar num pedal | Mensagem de falha, modo desativado sem travar |
| G8 | Desativar o modo e fazer um backup | O backup funciona normalmente (porta liberada) |

## Critério de aprovação da v1.0
Considere a v1.0 aprovada quando: **B3, D6 e todos os itens de C passarem**. Envie para a próxima fase:
- o tamanho real (B2);
- os hashes;
- o log de uma captura e de uma restauração;
- 2 ou 3 pares de dumps "antes/depois" (veja [SYSEX.md §5](SYSEX.md#5-como-capturar-dois-dumps-e-compará-los)).
