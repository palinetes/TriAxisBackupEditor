# Modo simples e modo avançado — desenho

Data: 2026-10-17 · Autor do pedido: @palinetes · Status: aprovado para implementação ("siga sua intuição, depois reviso").

## Objetivo
O programa precisa ser usável por quem **não entende de MIDI**. O usuário médio de TriAxis tem um cabo USB-MIDI simples
e nenhuma pedaleira MIDI; a maior dor é editar timbres pelo painel do aparelho e o maior medo é perder os timbres.
Usuários avançados (ex.: RME + FCB1010) continuam com todas as ferramentas.

## Estrutura
- Barra superior comum: identificação, selo de estado ("TriAxis pronto" / "Configurar ligação"), chave **Simples | Avançado**, Manual.
- **Modo avançado** = as abas atuais (Conexão, Controle, Backup, Restaurar, Biblioteca, Editor, Visualizar, Configurações, Log).
- **Modo simples** = navegação por páginas, sem jargão:
  - **Início**: "O que você quer fazer?" → **Meus timbres**, **Backup**, **Tocar**; mostra o último backup.
  - **Assistente de ligação** (primeira vez, ou pelo selo de estado): tipo de ligação (cabo USB-MIDI / placa de áudio com MIDI)
    → escolher o aparelho (entrada e saída pareadas pelo nome) → desenho dos cabos → **primeiro backup** (testa a entrada;
    se nada chegar: "inverta os dois plugues") → **teste de envio** (Program Change 1; se não mudar, ajustar o "número do TriAxis" 1–16).
  - **Meus timbres**: abre o último backup no painel do TriAxis (mesmo painel do editor avançado, extraído para um controle
    reutilizável), lista dos 90 timbres com **nomes** dados pelo usuário, Desfazer e **Mandar para o TriAxis**.
  - **Backup**: "Guardar a memória do TriAxis" (fluxo guiado) e "Devolver um backup" (lista amigável → envio seguro).
  - **Tocar**: pedaleira grande (banco ▲▼, programa atual), mouse/teclado; ativa ao entrar e desativa ao sair;
    pedais mostram o nome do timbre chamado pelo programa (via mapa de programas do último backup).
- **Envio seguro no modo simples** (substitui "digite RESTAURAR"): aviso claro → **backup de segurança obrigatório**
  (SHIFT + PRESET ▼) → "Sim, substituir" → envio → "O display piscou?". O modo avançado mantém a confirmação digitada.

## Dados novos (configurações locais)
- `UiMode` (Simple/Advanced), `FirstRunCompleted`, `PresetNames` (90 nomes; só no computador).

## Lógica testável (camada Application/Storage)
- `DevicePairing`: pareia entrada/saída pelo nome (ex.: "UM-ONE"/"UM-ONE", "Babyface Midi Port 1").
- `SafeReplaceFlow`: só libera "substituir" depois de um backup de segurança salvo nesta sessão de envio; monta a
  confirmação completa para o `RestoreService` (que continua revalidando tudo).
- `PresetNaming`: rótulo do pedal/timbre a partir do mapa de programas + nomes.

## Fora do escopo
Mudanças no protocolo/mapa SysEx; configuração global de loops/saídas da 2:90 (continua no roteiro do manual).

## Verificação
Testes unitários da lógica nova; capturas de tela de todas as páginas pelo harness; o executável publicado deve abrir,
alternar modos e navegar sem erro; manual atualizado com o modo simples.
