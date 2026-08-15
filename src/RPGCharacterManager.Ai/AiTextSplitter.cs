namespace RPGCharacterManager.Ai;

/// <summary>
/// Разбиение длинного текста на части, пригодные для одного обращения к модели.
///
/// Книга целиком в запрос не помещается, поэтому она разбирается частями. Резать
/// текст посреди предложения нельзя: описание оружия, разорванное пополам, не
/// станет объектом ни в одной из частей. Поэтому разрез ищется по границе абзаца,
/// а если её нет — по концу предложения.
/// </summary>
internal static class AiTextSplitter
{
    /// <summary>Желаемый размер одной части в знаках.</summary>
    public const int PartSize = 9000;

    /// <summary>Насколько раньше желаемого размера допускается искать границу.</summary>
    private const int SearchWindow = 2000;

    /// <summary>
    /// Разбивает текст на части.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="size">Желаемый размер одной части.</param>
    /// <returns>Части текста в порядке следования.</returns>
    public static IReadOnlyList<string> Split(string text, int size = PartSize)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var parts = new List<string>();
        var position = 0;

        while (position < text.Length)
        {
            var length = Math.Min(size, text.Length - position);

            if (position + length < text.Length)
            {
                length = FindBoundary(text, position, length);
            }

            var part = text.Substring(position, length).Trim();

            if (part.Length > 0)
            {
                parts.Add(part);
            }

            position += length;
        }

        return parts;
    }

    /// <summary>
    /// Находит длину части, оканчивающуюся на границе абзаца либо предложения.
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="start">Начало части.</param>
    /// <param name="length">Наибольшая допустимая длина.</param>
    /// <returns>Длина части.</returns>
    private static int FindBoundary(string text, int start, int length)
    {
        var minimum = Math.Max(1, length - SearchWindow);
        var window = text.AsSpan(start, length);

        var paragraph = window.LastIndexOf('\n');

        if (paragraph >= minimum)
        {
            return paragraph + 1;
        }

        var sentence = window.LastIndexOf('.');

        return sentence >= minimum ? sentence + 1 : length;
    }
}
