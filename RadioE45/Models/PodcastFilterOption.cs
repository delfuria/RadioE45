namespace RadioE45.Models;

// Voce di un Picker di filtro. Value null rappresenta l'opzione "Tutti/Tutte" (nessun filtro attivo).
public sealed record PodcastFilterOption<T>(string Label, T? Value) where T : struct
{
    public override string ToString() => Label;
}
