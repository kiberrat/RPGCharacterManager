using System.Numerics;
using RPGCharacterManager.Core.Models.Dice;

namespace RPGCharacterManager.Tests.Dice;

/// <summary>
/// Тела кубиков: форма, грани и расстановка чисел.
/// </summary>
public sealed class DieMeshTests
{
    public static TheoryData<int, DieShape> Shapes => new()
    {
        { 2, DieShape.Coin },
        { 3, DieShape.Barrel },
        { 4, DieShape.Tetrahedron },
        { 6, DieShape.Cube },
        { 8, DieShape.Octahedron },
        { 10, DieShape.Trapezohedron },
        { 12, DieShape.Dodecahedron },
        { 20, DieShape.Icosahedron },
        { 100, DieShape.Ball },
    };

    public static TheoryData<int> AllSides
    {
        get
        {
            var data = new TheoryData<int>();

            foreach (var sides in new[] { 2, 3, 4, 5, 6, 7, 8, 10, 12, 16, 20, 24, 30, 50, 100, 777 })
            {
                data.Add(sides);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Создание_СтандартныйКубик_ДаётПривычнуюФорму(int sides, DieShape expected)
    {
        var mesh = DieMesh.Create(sides);

        Assert.Equal(expected, mesh.Shape);
        Assert.Equal(sides, mesh.Sides);
    }

    [Theory]
    [MemberData(nameof(AllSides))]
    public void Создание_ЛюбоеЧислоГраней_ДаётКорректноеТело(int sides)
    {
        var mesh = DieMesh.Create(sides);

        Assert.NotEmpty(mesh.Faces);

        foreach (var face in mesh.Faces)
        {
            // Грань меньше чем из трёх вершин не имеет площади и не рисуется.
            Assert.True(face.Vertices.Count >= 3, $"Грань со значением {face.Value} вырождена.");

            Assert.False(
                float.IsNaN(face.Normal.X) || float.IsNaN(face.Normal.Y) || float.IsNaN(face.Normal.Z),
                $"Нормаль грани со значением {face.Value} не определена.");

            Assert.Equal(1, face.Normal.Length(), 3);

            // Тело выпукло и его центр совпадает с началом координат, поэтому
            // нормаль обязана смотреть в ту же сторону, что и центр грани.
            Assert.True(
                Vector3.Dot(face.Normal, Vector3.Normalize(face.Center)) > 0,
                $"Нормаль грани со значением {face.Value} смотрит внутрь тела.");

            foreach (var vertex in face.Vertices)
            {
                Assert.True(vertex.Length() <= 1.0001f, "Вершина вышла за пределы единичной сферы.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllSides))]
    public void ПоискГрани_ЗначениеКубика_НаходитСвоюГрань(int sides)
    {
        var mesh = DieMesh.Create(sides);
        var expected = Math.Min(sides, DieMesh.MaximumDistinctSides);

        for (var value = 1; value <= expected; value++)
        {
            var index = mesh.FindFaceIndex(value);

            Assert.True(index != DieMesh.NoFace, $"У кубика d{sides} нет грани со значением {value}.");
            Assert.Equal(value, mesh.Faces[index].Value);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    public void Значения_ПравильноеТело_НаПротивоположныхГранях(int sides)
    {
        const float Opposite = -0.999f;

        var mesh = DieMesh.Create(sides);

        foreach (var face in mesh.Faces)
        {
            var other = mesh.Faces.FirstOrDefault(
                candidate => Vector3.Dot(candidate.Normal, face.Normal) < Opposite);

            Assert.NotNull(other);

            // На игральных костях сумма чисел противоположных граней одинакова.
            Assert.Equal(sides + 1, face.Value + other.Value);
        }
    }

    [Fact]
    public void Грани_Куб_ЛежатНаОсях()
    {
        var mesh = DieMesh.Create(6);

        Assert.Equal(6, mesh.Faces.Count);
        Assert.All(mesh.Faces, face => Assert.Equal(4, face.Vertices.Count));
    }

    [Fact]
    public void Грани_Икосаэдр_Треугольные()
    {
        var mesh = DieMesh.Create(20);

        Assert.Equal(20, mesh.Faces.Count);
        Assert.All(mesh.Faces, face => Assert.Equal(3, face.Vertices.Count));
    }

    [Fact]
    public void Грани_Додекаэдр_Пятиугольные()
    {
        var mesh = DieMesh.Create(12);

        Assert.Equal(12, mesh.Faces.Count);
        Assert.All(mesh.Faces, face => Assert.Equal(5, face.Vertices.Count));
    }

    [Fact]
    public void Грани_Монета_ИмеютДваЧислаИОбодок()
    {
        var mesh = DieMesh.Create(2);

        Assert.Equal(2, mesh.Faces.Count(face => face.HasValue));
        Assert.Contains(mesh.Faces, face => !face.HasValue);
    }

    [Fact]
    public void Создание_МеньшеДвухГраней_ОтклоняетЗапрос() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DieMesh.Create(1));

    [Fact]
    public void Создание_ОдноИТоЖеЧислоГраней_ВозвращаетТоЖеТело() =>
        Assert.Same(DieMesh.Create(20), DieMesh.Create(20));
}
