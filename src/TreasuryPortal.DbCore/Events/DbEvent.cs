namespace TreasuryPortal.DbCore.Events;

/// <summary>Событие, которое функция базы отправила через pg_notify.</summary>
/// <param name="Channel">Канал, в который пришло событие.</param>
/// <param name="Payload">
/// Текст события как есть. Разбирает его получатель: слой данных формат события не знает.
/// </param>
public sealed record DbEvent(string Channel, string Payload);
