/**
 * Web endpoint for RPG Character Manager feedback.
 * The recipient is stored in Script Properties under RECIPIENT_EMAIL and is
 * never returned to the desktop application.
 */
function doPost(e) {
  try {
    const data = JSON.parse((e && e.postData && e.postData.contents) || '{}');

    // Honeypot: ordinary clients always send an empty website value.
    if (String(data.website || '').trim() !== '') {
      return json_({ ok: true });
    }

    const message = String(data.message || '').trim();
    const contact = String(data.contact || '').trim();
    if (message.length < 3 || message.length > 5000 || contact.length > 200) {
      return json_({ ok: false, error: 'invalid_request' });
    }

    const recipient = PropertiesService.getScriptProperties().getProperty('RECIPIENT_EMAIL');
    if (!recipient) {
      throw new Error('RECIPIENT_EMAIL is not configured');
    }

    const kindLabels = {
      Suggestion: 'Предложение',
      Bug: 'Ошибка',
      Question: 'Вопрос',
      Other: 'Другое'
    };
    const kind = kindLabels[String(data.kind || '')] || 'Другое';
    const subject = '[RPG Character Manager] ' + kind;
    const body = [
      'Категория: ' + kind,
      'Версия: ' + String(data.applicationVersion || 'не указана'),
      'Отправлено UTC: ' + String(data.sentAtUtc || new Date().toISOString()),
      'Контакт для ответа: ' + (contact || 'не указан'),
      '',
      message,
      '',
      'Технические сведения:',
      String(data.technicalInformation || 'не приложены')
    ].join('\n');

    GmailApp.sendEmail(recipient, subject, body, {
      name: 'RPG Character Manager',
      noReply: true
    });

    return json_({ ok: true });
  } catch (error) {
    console.error(error);
    return json_({ ok: false, error: 'server_error' });
  }
}

function doGet() {
  return json_({ ok: true, service: 'feedback' });
}

function json_(value) {
  return ContentService
    .createTextOutput(JSON.stringify(value))
    .setMimeType(ContentService.MimeType.JSON);
}