using TriAxisBackupEditor.Application.Logging;
using TriAxisBackupEditor.Midi;
using static TriAxisBackupEditor.Localization.Localizer;

namespace TriAxisBackupEditor.Application.Control;

/// <summary>
/// Modo controle: mantém a porta MIDI OUT aberta e envia Program Change / Control Change ao TriAxis.
/// Essas mensagens apenas selecionam programas ou acionam funções: não gravam nada na memória do equipamento.
/// Opcionalmente escuta uma entrada MIDI (pedaleira/controlador ligado ao PC) para disparar ações.
/// </summary>
public sealed class ControlSurfaceService : IDisposable
{
    private readonly IMidiDeviceService _midi;
    private readonly ITechLog _log;
    private readonly object _sync = new();
    private IMidiOutputPort? _output;
    private IMidiInputPort? _input;

    public ControlSurfaceService(IMidiDeviceService midi, ITechLog log)
    {
        _midi = midi;
        _log = log;
    }

    /// <summary>Mensagem recebida na entrada de controle (thread de fundo).</summary>
    public event EventHandler<ShortMessage>? InputReceived;

    public event EventHandler<string>? ErrorOccurred;

    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return _output is not null;
            }
        }
    }

    public MidiDeviceInfo? Output { get; private set; }

    public MidiDeviceInfo? Input { get; private set; }

    public void Start(MidiDeviceInfo output, MidiDeviceInfo? controlInput)
    {
        ArgumentNullException.ThrowIfNull(output);
        Stop();

        lock (_sync)
        {
            _output = _midi.OpenOutput(output);
            Output = output;
            try
            {
                if (controlInput is not null)
                {
                    _input = _midi.OpenInput(controlInput);
                    _input.ShortMessageReceived += OnShortMessage;
                    _input.ErrorOccurred += OnInputError;
                    Input = controlInput;
                }
            }
            catch
            {
                _output.Dispose();
                _output = null;
                Output = null;
                throw;
            }
        }

        _log.Info($"Modo controle ativado: MIDI OUT \"{output.DisplayName}\"" +
                  (controlInput is null ? "." : TrF($", entrada de controle \"{controlInput.DisplayName}\".")));
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_output is null && _input is null)
            {
                return;
            }

            if (_input is not null)
            {
                _input.ShortMessageReceived -= OnShortMessage;
                _input.ErrorOccurred -= OnInputError;
                _input.Dispose();
                _input = null;
            }

            _output?.Dispose();
            _output = null;
            Output = null;
            Input = null;
        }

        _log.Info("Modo controle desativado: portas MIDI fechadas.");
    }

    /// <param name="channel">1–16</param>
    /// <param name="program">1–128</param>
    public void SendProgramChange(int channel, int program)
    {
        var message = MidiMessages.ProgramChange(channel, program);
        Send(message);
        _log.Info($"Controle: Program Change {program} no canal {channel} (bytes {message & 0xFF:X2} {(message >> 8) & 0xFF:X2}).");
    }

    public void SendControlChange(int channel, int controller, int value)
    {
        var message = MidiMessages.ControlChange(channel, controller, value);
        Send(message);
        _log.Info($"Controle: CC {controller} = {value} no canal {channel}.");
    }

    public void Dispose() => Stop();

    private void Send(uint message)
    {
        lock (_sync)
        {
            if (_output is null)
            {
                throw new InvalidOperationException(Tr("Ative o modo controle antes de enviar mensagens."));
            }

            _output.SendShort(message);
        }
    }

    private void OnShortMessage(object? sender, MidiShortMessageEventArgs e)
    {
        var message = MidiMessages.Decode(e.Message);
        if (message.Kind != MidiMessageKind.Other)
        {
            InputReceived?.Invoke(this, message);
        }
    }

    private void OnInputError(object? sender, MidiErrorEventArgs e)
    {
        _log.Warn("Entrada de controle: " + e.Message);
        ErrorOccurred?.Invoke(this, e.Message);
    }
}
