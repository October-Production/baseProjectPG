using System.Collections.Concurrent;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace TreasuryPortal.Integrations.Soap;

/// <summary>
/// Классы контракта ↔ XElement через XmlSerializer. Сериализатор на тип создаётся один раз:
/// его конструктор дорогой, а простой конструктор XmlSerializer(Type) к тому же кэширует
/// сгенерированный код сам — другие конструкторы так не делают и копят сборки в памяти.
/// </summary>
internal static class SoapSerializer
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> Serializers = new();
    private static readonly ConcurrentDictionary<Type, SoapOperation> Operations = new();

    /// <summary>Без xmlns:xsi и xmlns:xsd в каждом запросе.</summary>
    private static readonly XmlSerializerNamespaces NoExtraNamespaces = new([XmlQualifiedName.Empty]);

    /// <summary>Имя операции и SOAPAction по атрибутам класса запроса.</summary>
    public static SoapOperation Operation(Type requestType) => Operations.GetOrAdd(requestType, static type =>
    {
        var root = type.GetCustomAttribute<XmlRootAttribute>();
        var name = string.IsNullOrEmpty(root?.ElementName) ? type.Name : root.ElementName;
        return new SoapOperation(name, type.GetCustomAttribute<SoapActionAttribute>()?.Action ?? "");
    });

    public static XElement Serialize(object value)
    {
        var document = new XDocument();
        using (var writer = document.CreateWriter())
        {
            For(value.GetType()).Serialize(writer, value, NoExtraNamespaces);
        }

        return document.Root ?? throw new InvalidOperationException($"{value.GetType().Name} не дал XML");
    }

    /// <summary>
    /// Ответ в класс контракта. Элемент не тот — исключение с понятным текстом: XmlSerializer прячет
    /// суть («… was not expected») во вложенное исключение, а в журнал попадает только верхнее.
    /// </summary>
    public static T Deserialize<T>(XElement element)
    {
        try
        {
            using var reader = element.CreateReader();
            return (T?)For(typeof(T)).Deserialize(reader)
                ?? throw new InvalidOperationException($"Пустой {typeof(T).Name}");
        }
        catch (InvalidOperationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"Ответ {element.Name} не разобрать в {typeof(T).Name}: {ex.InnerException.Message}", ex);
        }
    }

    private static XmlSerializer For(Type type) => Serializers.GetOrAdd(type, static t => new XmlSerializer(t));
}

/// <param name="Name">Имя операции — элемент запроса; по нему же ищется Actions в настройках.</param>
/// <param name="Action">SOAPAction из атрибута.</param>
internal sealed record SoapOperation(string Name, string Action);
