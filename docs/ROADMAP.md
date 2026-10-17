# Roadmap

Prioridade absoluta, nesta ordem:
1. capturar um dump real sem corrompê-lo;
2. salvá-lo de forma confiável;
3. restaurá-lo com segurança;
4. só depois editar parâmetros.

## Fase 1: Backup e restauração ✅ (v1.0)
- Detecção automática e seleção manual de MIDI IN/OUT (WinMM), com atualização a cada 3 s e tratamento de desconexão.
- Teste de conexão passivo: abre as portas e escuta, sem enviar nada.
- Captura com remontagem de fragmentos, timeouts configuráveis (início 60 s, recepção 10 s), cancelamento e arquivo `.syx.part` para recepções incompletas.
- Gravação sem sobrescrever, verificação SHA-256 após gravar e pasta alternativa se a pasta escolhida falhar.
- Restauração validada, com confirmação em duas etapas, backup de segurança oferecido, envio em blocos (128 bytes / 5–50 ms) e revalidação do hash antes do envio.
- Log técnico em arquivo (liga/desliga).
- **Pendente:** executar o [plano de testes com o TriAxis real](PLANO_DE_TESTES.md).

## Fase 2: Biblioteca e comparação de dumps ✅ (v1.0, base)
- SQLite com nome, data, tamanho, SHA-256, firmware (quando detectável), observação, origem e cabeçalho.
- Busca, importação, duplicação, renomeação, observação, exclusão para a Lixeira e exportação CSV.
- Comparação por hash e diferenças byte a byte, com região do mapa preliminar.
- **Próximo:** diff lado a lado preset por preset, com nomes de parâmetro e valores "antes → depois" (depende da fase 3); marcar um backup como "referência"; tags.

## Fase 3: Parser de presets 🟡 (mapa preliminar só para leitura)
- Hoje: leitura dos 90 presets e dos 128 programas pelo mapa do editor de referência, sem adivinhar valores desconhecidos.
- **Próximos passos, depois de capturar dumps reais:**
  1. confirmar determinismo (3 backups seguidos com hash idêntico);
  2. fazer os experimentos de uma alteração por vez de [SYSEX.md §4](SYSEX.md#4-plano-para-identificar-offsets-com-segurança) para cada parâmetro, modo, switch e mapa de programas;
  3. fazer a varredura de valores do knob para obter a tabela real;
  4. investigar o byte `0x04` e a região 995–1124 (mapa de programas não confirmado e 2 bytes desconhecidos), começando pelas configurações globais de loops e saídas usadas com a Simul-Class 2:90 (roteiro na seção 11 do [manual](manual/index.html#290));
  5. procurar um checksum (alterar 1 parâmetro e verificar se algum outro byte muda junto);
  6. adicionar os dumps reais como *fixtures* nos testes;
  7. trocar `TriAxisDumpLayout.IsPreliminary` para `false`.

## Fase 4: Editor visual 🟡 (v1.0, sobre o mapa preliminar)
- ✅ Painel no estilo do frontal do TriAxis, a pedido do usuário: displays de 7 segmentos com ◄ ►, PRESET, PROGRAM/CHANNEL + PGM/CH, matriz de LEDs de modo e FX/SW1–4.
- ✅ Edição sobre **cópia** de um dump real; o resultado é sempre um arquivo novo (`TriAxis_Editado_...syx`).
- ✅ Lista de alterações (antes → depois), copiar/colar/desfazer preset e matriz dos 90 presets.
- ✅ Envio pelo mesmo fluxo de restauração (duas etapas + aviso de mapa preliminar).
- ✅ Preservação bit a bit de cabeçalho, região final, F7 e bits desconhecidos (coberta por testes).
- ⏳ Trocar o aviso de "mapa preliminar" por "validado" quando a fase 3 terminar.
- ⏳ Edição do mapa de programas e das configurações de loops/saídas (região 995–1124), depois de mapeada.

## Fase 4b: Modo controle (pedaleira) ✅ (v1.0)
- Program Change no canal configurado, com bancos estilo FCB1010 (4/5/8/10 pedais por banco), banco ▲▼ e programa ±1.
- Tela de pedaleira com footswitches, LED do pedal ativo e displays de banco/programa.
- 4 botões CC configuráveis (127/0).
- Entradas: atalhos de teclado e **MIDI learn** de um controlador ligado ao PC; opção de repassar Program Change.
- **Próximo:** pedal de expressão (CC contínuo com curva/limites), setlists (sequência de programas por música), cenas que enviam PC ao TriAxis e ao G-Major ao mesmo tempo, modo tela cheia para o palco.

## Fase 4c: Modo simples ✅ (v1.0)
- Para quem não entende de MIDI: assistente de ligação, tela inicial com Meus timbres / Backup / Tocar, nomes dos timbres e envio seguro com backup de segurança obrigatório.
- Desenho: [docs/superpowers/specs/2026-10-17-modo-simples-design.md](superpowers/specs/2026-10-17-modo-simples-design.md).
- **Próximo:** testar com usuários reais (pessoas sem experiência com MIDI) e ajustar textos e passos conforme as dúvidas.

## Fase 5: Importação/exportação CSV ⏳
- Hoje: exportação CSV da **biblioteca**.
- Próximo: exportar os **presets** de um dump para CSV/planilha (documentação de rig, impressão para show).
- Importar CSV de presets para um dump, com validação rígida de cada valor e geração de arquivo novo (depende da fase 4).

## Fase 6: Automação de backup ⏳
- Lembrete periódico ("último backup há 30 dias").
- Modo "escuta": salvar automaticamente qualquer dump recebido enquanto o programa está aberto.
- Backup de segurança obrigatório (sem opção de pular) antes de restaurações, configurável.
- Cópia automática da pasta de backups para um segundo destino local (pendrive/HD externo). Continua sem internet.

## Fase 7: Suporte a múltiplos TriAxis ⏳
- Perfis de equipamento (ex.: "TriAxis estúdio", "TriAxis turnê") com portas MIDI, observações e firmware próprios.
- Biblioteca filtrada por equipamento e alerta ao restaurar o backup de um equipamento em outro.
- Identificação por firmware/byte de cabeçalho, se a fase 3 descobrir algo que distinga as unidades.
