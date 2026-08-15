using System.Text;

namespace RPGCharacterManager.Import;

/// <summary>
/// Чтение текстовых файлов чужого происхождения.
///
/// Кодировку файл не сообщает, а угадать её можно только по содержимому.
/// Порядок проверки таков: метка порядка байтов, затем UTF-8, затем однобайтовая
/// кодировка Windows. Молчаливая замена испорченных знаков недопустима — русский
/// текст превратился бы в набор вопросительных знаков, и разбор пошёл бы
/// по бессмыслице.
/// </summary>
internal static class TextEncodings
{
    /// <summary>Кодовая страница однобайтовой кириллицы Windows.</summary>
    private const int CyrillicCodePage = 1251;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly Lazy<Encoding?> Cyrillic = new(CreateCyrillic);

    /// <summary>
    /// Переводит содержимое файла в текст.
    /// </summary>
    /// <param name="bytes">Содержимое файла.</param>
    /// <returns>Текст и название распознанной кодировки.</returns>
    public static (string Text, string Encoding) Decode(byte[] bytes)
    {
        if (HasUtf8Mark(bytes))
        {
            return (StrictUtf8.GetString(bytes, 3, bytes.Length - 3), "UTF-8");
        }

        try
        {
            return (StrictUtf8.GetString(bytes), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            // Файл не в UTF-8: скорее всего это однобайтовая кириллица,
            // в которой до сих пор сохраняют текст многие редакторы Windows.
            var cyrillic = Cyrillic.Value;

            return cyrillic is null
                ? (Encoding.Latin1.GetString(bytes), "Latin-1")
                : (cyrillic.GetString(bytes), cyrillic.WebName);
        }
    }

    private static bool HasUtf8Mark(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static Encoding? CreateCyrillic()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            return Encoding.GetEncoding(CyrillicCodePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            // Однобайтовые кодировки недоступны: остаётся посимвольное чтение,
            // при котором хотя бы латиница и цифры останутся читаемыми.
            return null;
        }
    }
}
