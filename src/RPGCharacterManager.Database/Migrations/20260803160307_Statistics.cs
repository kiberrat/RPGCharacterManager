using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Statistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Amount",
                table: "History",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "History",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_History_Action_Subject",
                table: "History",
                columns: new[] { "Action", "Subject" });

            // Записи, сделанные до появления этих полей, тоже должны считаться.
            // Название и величину можно достать обратно из описания и значений
            // только потому, что их составило само приложение по неизменному
            // образцу: «Ресурс (причина)», «Применено «Заклинание» (уровень)»,
            // «Оружие: попадание …». Разбор чужого текста здесь был бы гаданием,
            // и его тут нет.

            // Изменение ресурса: название — начало описания. Отделяется и скобка
            // с причиной — «Здоровье (Длительный отдых)», — и двоеточие: ранние
            // версии приложения дописывали в описание сами значения,
            // «Здоровье: 28 → 19 (изменено на листе)». Берётся то из двух,
            // что встретилось раньше.
            migrationBuilder.Sql(
                """
                UPDATE History
                SET Subject = CASE
                        WHEN instr(Description, ': ') > 0
                             AND (instr(Description, ' (') = 0
                                  OR instr(Description, ': ') < instr(Description, ' ('))
                            THEN substr(Description, 1, instr(Description, ': ') - 1)
                        WHEN instr(Description, ' (') > 0
                            THEN substr(Description, 1, instr(Description, ' (') - 1)
                        ELSE Description
                    END
                WHERE Action = 'изменение_ресурса' AND Description IS NOT NULL AND Subject IS NULL;
                """);

            // Применение заклинания и атака оружием: название в кавычках
            // и до двоеточия соответственно.
            migrationBuilder.Sql(
                """
                UPDATE History
                SET Subject = substr(
                        Description,
                        instr(Description, '«') + 1,
                        instr(Description, '»') - instr(Description, '«') - 1)
                WHERE Action = 'применение_заклинания'
                  AND Subject IS NULL
                  AND instr(Description, '»') > instr(Description, '«')
                  AND instr(Description, '«') > 0;
                """);

            migrationBuilder.Sql(
                """
                UPDATE History
                SET Subject = substr(Description, 1, instr(Description, ': ') - 1)
                WHERE Action = 'атака_оружием' AND Subject IS NULL AND instr(Description, ': ') > 0;
                """);

            // Величина события. Числа записаны в виде, принятом в системе
            // пользователя, поэтому запятая приводится к точке: разделитель
            // тысяч приложение не использует, и другой разницы между видами нет.
            migrationBuilder.Sql(
                """
                UPDATE History
                SET Amount = CAST(replace(NewValue, ',', '.') AS REAL)
                             - CAST(replace(OldValue, ',', '.') AS REAL)
                WHERE Action = 'изменение_ресурса'
                  AND Amount IS NULL
                  AND OldValue IS NOT NULL
                  AND NewValue IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE History
                SET Amount = CAST(replace(NewValue, ',', '.') AS REAL)
                WHERE Action IN ('атака_оружием', 'применение_заклинания')
                  AND Amount IS NULL
                  AND NewValue IS NOT NULL;
                """);

            // Критические попадания получают свой код действия: до этой миграции
            // они лежали среди обычных атак и опознавались только по описанию.
            migrationBuilder.Sql(
                """
                UPDATE History
                SET Action = 'критическое_попадание'
                WHERE Action = 'атака_оружием' AND instr(Description, 'критическое попадание') > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_History_Action_Subject",
                table: "History");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "History");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "History");
        }
    }
}
