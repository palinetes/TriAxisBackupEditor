# SysEx do TriAxis: formato, mapa preliminar e plano de validação

> **Status do mapa:** PRELIMINAR nos parâmetros. Vem do editor web [markphilpot/triaxiseditor](https://github.com/markphilpot/triaxiseditor) (`js/triaxis.js`, GPL-3.0; mesmo código publicado em triaxiseditor.com). O **tamanho e a estrutura** do dump (1.126 bytes, 90 presets × 11 bytes) foram confirmados num TriAxis real em 2026-10-16. O **significado de cada byte** dos presets ainda não foi conferido parâmetro por parâmetro. Ele é usado para **visualizar** e pelo **Editor**, que só trabalha sobre cópias e sempre gera arquivos novos. Confie no editor para um parâmetro somente depois de validá-lo pelo plano abaixo e pela seção F do [Plano de Testes](PLANO_DE_TESTES.md).

## 1. O que é SysEx

*System Exclusive* (SysEx) é o tipo de mensagem MIDI que cada fabricante usa para dados próprios do equipamento: dumps de memória, parâmetros, firmware. Ao contrário de Note On ou Program Change, não tem tamanho fixo.

### Formato `F0 … F7`

```
F0  <ID do fabricante>  <dados ...>  F7
│   │                   │            └─ EOX: fim da mensagem
│   │                   └─ somente bytes 00–7F (bit 7 sempre 0)
│   └─ 1 byte, ou 3 bytes começando com 00 (IDs estendidos)
└─ início de SysEx
```

Regras usadas pelo validador (`SysExValidator`):

| Regra | Se violada |
|---|---|
| Arquivo não vazio | **Erro** (bloqueia o envio) |
| Primeiro byte `F0` | **Erro** |
| Último byte `F7` | **Erro** (dump truncado) |
| Entre `F0` e `F7`, apenas `00`–`7F` | **Erro** (`STATUS_INVALIDO`) |
| Nenhum `F0` novo antes do `F7` anterior | **Erro** (`F0_SEM_F7`) |
| Nenhum byte fora de mensagem | **Erro** (`FORA_SYSEX`) |
| Bytes de tempo real `F8`–`FF` dentro do arquivo | **Erro** (um `.syx` não deveria contê-los) |
| Cabeçalho `F0 00 00 4A 02` | Aviso (`CABECALHO`) |
| Tamanho 1.126 bytes (dump de um TriAxis real) | Aviso (`TAMANHO`) |
| Uma única mensagem | Aviso (`MULTIPLAS_MSG`) |

Erros bloqueiam a restauração. Avisos exigem uma confirmação extra: o mapa preliminar pode estar errado, e um backup legítimo do seu TriAxis não deve ser impedido por ele.

### Fragmentação na recepção

O driver do Windows entrega SysEx longo em **pedaços do tamanho do buffer** (`MIM_LONGDATA`). O programa nunca assume que a mensagem chega inteira:

1. `WinMmMidiInputPort` mantém 16 buffers de 4 KB. O callback do driver só **copia** os bytes e devolve o controle. Eventos e reenfileiramento dos buffers rodam numa thread própria.
2. `SysExAssembler` recebe os fragmentos na ordem e reconstrói as mensagens `F0…F7`. Ele descarta bytes de tempo real intercalados (permitidos pela especificação MIDI) e aborta mensagens interrompidas por um byte de status.
3. `BackupCaptureService` considera o dump completo só depois do `F7` **e** de 1,5 s de silêncio, para o caso de o equipamento mandar mais de uma mensagem.

## 2. Estrutura do dump

Um dump completo é enviado ao pressionar **SHIFT + PRESET DOWN** no painel. Um TriAxis real (backups de 2026-10-16 e 2026-10-17, capturados por este programa) mandou **uma mensagem de 1.126 bytes**. Três backups seguidos deram o **mesmo SHA-256**, então o dump é determinístico:

| Offset (hex) | Offset (dec) | Tamanho | Conteúdo | Status |
|---|---|---|---|---|
| `0x0000` | 0 | 1 | `F0` início de SysEx | confirmado |
| `0x0001` | 1 | 3 | `00 00 4A`: ID estendido do fabricante | confirmado |
| `0x0004` | 4 | 1 | `02`: significado desconhecido (modelo? versão? canal?) | confirmado o valor |
| `0x0005`–`0x03E2` | 5–994 | 990 | **90 presets × 11 bytes** | tamanho confirmado; parâmetros, ver abaixo |
| `0x03E3`–`0x0462` | 995–1122 | 128 | Segundo a referência, **mapa programa→preset** (1 byte por programa 1–128) | **não confirmado** |
| `0x0463`–`0x0464` | 1123–1124 | 2 | Desconhecido (no aparelho real: `01 00`) | desconhecido |
| `0x0465` | 1125 | 1 | `F7` fim | confirmado |

**Região 995–1124:** no aparelho real ela tinha `14` seguido de zeros e `01 00`. Isso não parece um mapa de programas (seria "o programa 1 chama o preset 21 e todos os outros chamam o preset 1"). Por isso o programa **preserva esses bytes como estão**, mas não os edita nem os usa para dar nome aos pedais até que um experimento confirme o significado (`TriAxisDump.ProgramMapLooksValid` exige pelo menos 10 valores diferentes, todos menores que 90).

Não há checksum conhecido.

### Arquivos do editor web (2.746 bytes)

O editor web de referência supunha um dump de 2.746 bytes, com um bloco de 1.622 bytes de "controladores" depois do mapa de programas. Os arquivos que ele grava são, na verdade, **o dump real de 1.126 bytes, seguido de 1.619 zeros e de outro `F7`**. Um arquivo assim não é SysEx válido do jeito que está (o primeiro `F7` aparece no meio).

`TriAxisDumpFile.Normalize` reconhece **somente esse caso exato**: 2.746 bytes, cabeçalho certo, `F7` no offset 1125, todo o resto zero e `F7` no fim. Aí usa os primeiros 1.126 bytes. O arquivo em disco nunca é alterado:
- **Abrir, visualizar, comparar e enviar** usam os 1.126 bytes. O envio confere de novo o hash do arquivo e o dos 1.126 bytes antes de mandar.
- **Importar para a biblioteca** cria uma cópia nova `<nome>_dump-TriAxis.syx` com os 1.126 bytes e mantém o original intocado.
- Qualquer byte diferente de zero no enchimento faz o arquivo ser tratado como está, e o validador o bloqueia.

### Os 11 bytes de cada preset (segundo a referência)

| Byte | Parâmetro | Valores |
|---|---|---|
| 0 | Dynamic Voice | tabela de knob |
| 1 | Presence | tabela de knob |
| 2 | Master | tabela de knob |
| 3 | Lead 2 Drive | tabela de knob |
| 4 | Lead 1 Drive | tabela de knob |
| 5 | Bass | tabela de knob |
| 6 | Middle | tabela de knob |
| 7 | Treble | tabela de knob |
| 8 | Gain | tabela de knob |
| 9 | Modo (LEDs) | `7E` Rhythm Green, `7D` Rhythm Yellow, `7B` Lead 1 Green, `77` Lead 1 Yellow, `6F` Lead 1 Red, `5F` Lead 2 Green, `3F` Lead 2 Yellow, `7F` Lead 2 Red |
| 10 | FX / Switches | bit0 FX Loop, bit1 SW1, bit2 SW2, bit3 SW3, bit4 SW4 |

**Tabela de knob da referência** (16 posições, não lineares):

| Valor | `00` | `01` | `02` | `03` | `04` | `05` | `06` | `07` | `08` | `09` | `0A` | `0B` | `0C` | `0D` | `0E` | `0F` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Display | 0.0 | 1.0 | 2.0 | 3.0 | 3.5 | 4.0 | 4.5 | 5.0 | 5.5 | 6.0 | 6.5 | 7.0 | 7.5 | 8.0 | 9.0 | 10 |

O que esse mapa **ainda não explica** e precisa ser verificado:
- a tabela não tem 8.5 nem 9.5, e as posições abaixo de 3.0 são esparsas. Será que a resolução real é maior?
- o byte de modo usa um padrão de bits incomum: `7F` = Lead 2 Red, enquanto os outros têm um bit zerado;
- o significado do byte `0x04` (`02`);
- o conteúdo da região 995–1124 (mapa de programas? configurações de loops e saídas?).

O parser (`TriAxisDumpParser`) **não adivinha**: valores fora das tabelas aparecem como `? (0xNN)`, e o preset fica marcado como "valores desconhecidos".

## 3. Pontos de extensão para o parser real

| Onde | Para quê |
|---|---|
| `src/TriAxisBackupEditor.SysEx/TriAxisDumpLayout.cs` | Offsets, tamanhos e cabeçalho. Ao validar, troque `IsPreliminary` para `false` e atualize `Source`. |
| `src/TriAxisBackupEditor.SysEx/TriAxisDump.cs` (`TriAxisDumpParser`) | Tabelas de valores (knob, modo, FX) e a leitura dos presets e do mapa de programas. |
| `src/TriAxisBackupEditor.SysEx/SysExValidator.cs` | Regras específicas do TriAxis (hoje só avisos). Quando o mapa for confirmado, `TAMANHO` e `CABECALHO` podem passar a erro. |
| `src/TriAxisBackupEditor.SysEx/DumpComparer.cs` + `TriAxisDumpLayout.DescribeOffset` | Nomes de região na comparação. É a principal ferramenta para descobrir offsets. |
| `src/TriAxisBackupEditor.SysEx/TriAxisDumpEditor.cs` + `TriAxisValues.cs` | Editor (fase 4): edita uma cópia, só nas regiões mapeadas, com valores das tabelas. Ao validar/corrigir o mapa, ajuste aqui e nos testes `TriAxisDumpEditorTests`. |

Regras seguidas pelo editor (`TriAxisDumpEditor`), já implementadas e cobertas por testes:
1. Partir **sempre de uma cópia** de um dump real capturado, nunca gerar um dump do zero.
2. Alterar **somente** bytes de offsets validados e com valores das tabelas validadas.
3. Preservar a região 995–1124 e qualquer byte desconhecido **bit a bit**.
4. Validar o resultado com `SysExValidator` e mostrar ao usuário um diff (`DumpComparer`) antes de restaurar.
5. Se for descoberto um checksum, recalculá-lo e cobri-lo com testes que usem dumps reais.

## 4. Plano para identificar offsets com segurança

O método é **mudar uma coisa por vez e comparar dumps**. Nada é enviado ao TriAxis nessa fase, só backups (leitura).

1. **Linha de base e determinismo.** Faça **3 backups seguidos** sem tocar no TriAxis. Os três hashes SHA-256 devem ser **idênticos**. Se não forem, existe algo variável no dump (contador, estado atual): compare os três e anote os offsets que mudam sozinhos antes de continuar.
2. **Uma alteração por experimento.** Para cada parâmetro:
   1. escolha um preset (ex.: 1) e anote o valor atual do knob;
   2. mude **só** aquele parâmetro e salve o preset no TriAxis;
   3. faça o backup e escreva na observação, ex.: "P01 Gain 5.0→7.0";
   4. compare com o backup anterior (**Biblioteca → Comparar**).
3. **Critérios de confirmação de um offset:**
   - só 1 byte mudou (ou um conjunto pequeno e explicável), e ele está onde o mapa prevê (ex.: preset 1, byte 8 = Gain = offset `0x000D`);
   - o novo valor bate com a tabela (7.0 → `0B`);
   - o experimento repetido em **outro preset** (ex.: 37) muda o mesmo byte relativo (`5 + 36×11 + 8`);
   - voltar o knob ao valor original volta o hash ao do backup de antes.
4. **Varredura de valores do knob.** Para um parâmetro, grave o mesmo preset em todas as posições possíveis (0, 1, 2, 3, 3.5 … 10, mais as intermediárias que o painel permitir) e monte a tabela real de valor→display.
5. **Modo e switches.** Repita para cada LED de modo e para FX loop, SW1–SW4.
6. **Mapa de programas.** Mude no TriAxis o preset associado ao Program Change N e confirme que só o byte `995 + (N−1)` muda.
7. **Região final (995–1124).** Mude a atribuição de um programa MIDI (ex.: programa 2 → preset 5) e, em outro experimento, uma configuração de loop/saída. Anote quais offsets da região `995–1124` mudam. Só então dê nomes a eles.
8. **Restauração de ida e volta (essencial antes de qualquer edição):**
   1. backup A;
   2. altere vários presets à mão;
   3. backup B;
   4. restaure A;
   5. backup C.

   **C deve ter o mesmo SHA-256 de A.** Isso prova que capturar e restaurar é bit a bit fiel.
9. Registre cada experimento (data, preset, mudança, offsets, valores) numa planilha ou no CSV da biblioteca. Quando todos os itens acima passarem, atualize `TriAxisDumpLayout`, troque `IsPreliminary` para `false`, adicione os dumps reais como fixtures de teste e libere a fase 4 (editor).

## 5. Como capturar dois dumps e compará-los

1. Ligue: TriAxis **MIDI OUT** → interface **MIDI IN**. Selecione as portas em **Conexão**.
2. **Backup → observação "antes" → Fazer backup → SHIFT + PRESET DOWN.**
3. No TriAxis, faça **uma** alteração e salve-a no preset.
4. **Backup → observação "depois: <o que mudou>" → Fazer backup → SHIFT + PRESET DOWN.**
5. **Biblioteca**: selecione os dois com **Ctrl+clique** e clique em **Comparar 2 selecionados**.
6. Leia o resultado:
   - `IDÊNTICOS`: a alteração não foi salva, ou não faz parte do dump;
   - `DIFERENTES — N byte(s)`: a lista mostra `offset  A → B  região`. Com o mapa preliminar, a região indica preset e parâmetro (ex.: `Preset 01 · Gain`).
7. Para ver o contexto, abra os dois em **Visualizar dump** (hex + ASCII) e localize o offset.

Ferramentas externas também servem (por exemplo, `fc /b a.syx b.syx` no Prompt de Comando, que lista os bytes diferentes em hexadecimal).
