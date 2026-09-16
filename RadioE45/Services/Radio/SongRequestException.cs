namespace RadioE45.Services.Radio;

// Messaggio già pronto per la UI (es. testo di cooldown restituito dal server AzuraCast su una
// richiesta rifiutata) — SafeExecuteAsync lo userebbe come suffisso, qui viene mostrato as-is.
public class SongRequestException(string message) : Exception(message);
