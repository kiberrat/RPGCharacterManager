using System.Collections.Concurrent;
using System.Numerics;

namespace RPGCharacterManager.Core.Models.Dice;

/// <summary>
/// Форма кубика: тело, которым кубик показан на экране.
///
/// Форма выбирается по количеству граней и не задаётся пользователем: кубик с шестью
/// гранями — куб, с двадцатью — икосаэдр, а кубик с произвольным числом граней
/// получает бочонок или шар. Так пользовательский «Кристалл судьбы d777»
/// бросается наравне со встроенными кубиками.
/// </summary>
public enum DieShape
{
    /// <summary>Монета: два плоских числа и ободок.</summary>
    Coin = 0,

    /// <summary>Бочонок: числа на боковых гранях, торцы пустые.</summary>
    Barrel = 1,

    /// <summary>Тетраэдр: четыре треугольные грани.</summary>
    Tetrahedron = 2,

    /// <summary>Куб: шесть квадратных граней.</summary>
    Cube = 3,

    /// <summary>Октаэдр: восемь треугольных граней.</summary>
    Octahedron = 4,

    /// <summary>Трапецоэдр: десять граней-змеев — обычный вид кубика d10.</summary>
    Trapezohedron = 5,

    /// <summary>Додекаэдр: двенадцать пятиугольных граней.</summary>
    Dodecahedron = 6,

    /// <summary>Икосаэдр: двадцать треугольных граней.</summary>
    Icosahedron = 7,

    /// <summary>Шар: множество мелких граней для кубиков с большим числом значений.</summary>
    Ball = 8,
}

/// <summary>
/// Грань кубика.
/// </summary>
public sealed class DieFace
{
    /// <summary>
    /// Создаёт грань кубика.
    /// </summary>
    /// <param name="vertices">Вершины грани в порядке обхода против часовой стрелки снаружи.</param>
    /// <param name="value">Число на грани; ноль — грань без числа.</param>
    internal DieFace(IReadOnlyList<Vector3> vertices, int value)
    {
        Vertices = vertices;
        Value = value;

        var center = Vector3.Zero;

        foreach (var vertex in vertices)
        {
            center += vertex;
        }

        Center = center / vertices.Count;
        Normal = ComputeNormal(vertices, Center);
    }

    /// <summary>Вершины грани в порядке обхода против часовой стрелки, если смотреть снаружи.</summary>
    public IReadOnlyList<Vector3> Vertices { get; }

    /// <summary>Центр грани.</summary>
    public Vector3 Center { get; }

    /// <summary>Внешняя нормаль грани.</summary>
    public Vector3 Normal { get; }

    /// <summary>Число, написанное на грани. Ноль означает грань без числа.</summary>
    public int Value { get; }

    /// <summary>Грань несёт число.</summary>
    public bool HasValue => Value > 0;

    /// <summary>
    /// Вычисляет нормаль грани по способу Ньюэлла.
    ///
    /// Способ учитывает все вершины сразу, поэтому даёт верное направление и для
    /// граней-змеев трапецоэдра, у которых центр не лежит на нормали, и для граней,
    /// три соседние вершины которых почти лежат на одной прямой.
    /// </summary>
    /// <param name="vertices">Вершины грани.</param>
    /// <param name="center">Центр грани.</param>
    /// <returns>Единичная нормаль, направленная наружу.</returns>
    private static Vector3 ComputeNormal(IReadOnlyList<Vector3> vertices, Vector3 center)
    {
        var normal = Vector3.Zero;

        for (var index = 0; index < vertices.Count; index++)
        {
            var current = vertices[index];
            var next = vertices[(index + 1) % vertices.Count];

            normal += new Vector3(
                (current.Y - next.Y) * (current.Z + next.Z),
                (current.Z - next.Z) * (current.X + next.X),
                (current.X - next.X) * (current.Y + next.Y));
        }

        normal = Vector3.Normalize(normal);

        // Тело выпуклое и его центр совпадает с началом координат, поэтому наружу
        // смотрит та нормаль, которая согласована с направлением на центр грани.
        return Vector3.Dot(normal, center) < 0 ? -normal : normal;
    }
}

/// <summary>
/// Тело кубика: набор граней с числами.
///
/// Вершины уложены в единичную сферу, поэтому представление задаёт кубику
/// только положение и размер и не зависит от его формы.
/// </summary>
public sealed class DieMesh
{
    /// <summary>Наименьшее количество граней кубика.</summary>
    public const int MinimumSides = 2;

    /// <summary>
    /// Наибольшее количество граней, при котором каждое значение получает
    /// собственную грань. Кубик с большим числом значений показывается шаром,
    /// и выпавшее число пишется на грани, оказавшейся перед зрителем.
    /// </summary>
    public const int MaximumDistinctSides = 320;

    /// <summary>Количество граней, начиная с которого кубик показывается шаром.</summary>
    private const int BallThreshold = 25;

    /// <summary>Количество боковых граней бочонка, при котором он ещё читаем.</summary>
    private const int MaximumBarrelSides = BallThreshold - 1;

    private static readonly ConcurrentDictionary<int, DieMesh> Cache = new();

    private readonly Dictionary<int, int> _byValue;

    private DieMesh(DieShape shape, int sides, IReadOnlyList<DieFace> faces)
    {
        Shape = shape;
        Sides = sides;
        Faces = faces;

        _byValue = [];

        for (var index = 0; index < faces.Count; index++)
        {
            if (faces[index].HasValue)
            {
                _byValue.TryAdd(faces[index].Value, index);
            }
        }
    }

    /// <summary>Форма кубика.</summary>
    public DieShape Shape { get; }

    /// <summary>Количество значений кубика.</summary>
    public int Sides { get; }

    /// <summary>Грани тела, включая грани без чисел.</summary>
    public IReadOnlyList<DieFace> Faces { get; }

    /// <summary>
    /// Возвращает тело кубика с указанным количеством граней.
    /// Тела кэшируются: одна и та же форма строится один раз за время работы.
    /// </summary>
    /// <param name="sides">Количество значений кубика.</param>
    /// <returns>Тело кубика.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Граней меньше двух.</exception>
    public static DieMesh Create(int sides)
    {
        if (sides < MinimumSides)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sides),
                sides,
                $"У кубика должно быть не менее {MinimumSides} граней.");
        }

        return Cache.GetOrAdd(sides, Build);
    }

    /// <summary>Номер отсутствующей грани.</summary>
    public const int NoFace = -1;

    /// <summary>
    /// Возвращает номер грани с указанным числом.
    ///
    /// У шара граней меньше, чем значений у кубика с большим числом сторон,
    /// поэтому грань может отсутствовать: тогда выпавшее число пишется
    /// представлением на грани, оказавшейся перед зрителем.
    /// </summary>
    /// <param name="value">Выпавшее число.</param>
    /// <returns>Номер грани либо <see cref="NoFace"/>.</returns>
    public int FindFaceIndex(int value) => _byValue.TryGetValue(value, out var index) ? index : NoFace;

    private static DieMesh Build(int sides) => sides switch
    {
        2 => BuildCoin(),
        4 => BuildSolid(DieShape.Tetrahedron, Solids.Tetrahedron, 4),
        6 => BuildSolid(DieShape.Cube, Solids.Cube, 6),
        8 => BuildSolid(DieShape.Octahedron, Solids.Octahedron, 8),
        10 => BuildTrapezohedron(),
        12 => BuildSolid(DieShape.Dodecahedron, Solids.Dodecahedron, 12),
        20 => BuildSolid(DieShape.Icosahedron, Solids.Icosahedron, 20),
        <= MaximumBarrelSides => BuildBarrel(sides),
        _ => BuildBall(sides),
    };

    /// <summary>
    /// Собирает выпуклый многогранник по одним лишь вершинам.
    ///
    /// Грань — это опорная плоскость тела: через три вершины проводится плоскость,
    /// и если все остальные вершины лежат по одну сторону от неё, найдена грань,
    /// а её вершины — все, кто на этой плоскости оказался.
    ///
    /// Перечислять грани или направления на них вручную нельзя: у додекаэдра
    /// и икосаэдра ошибиться в такой таблице легче всего, а ошибка проявляется
    /// не поломкой сборки, а исчезнувшей гранью на экране.
    /// </summary>
    /// <param name="shape">Форма кубика.</param>
    /// <param name="vertices">Вершины тела.</param>
    /// <param name="sides">Количество значений кубика.</param>
    /// <returns>Тело кубика.</returns>
    private static DieMesh BuildSolid(DieShape shape, IReadOnlyList<Vector3> vertices, int sides)
    {
        const float PlaneTolerance = 0.0001f;
        const float MinimumArea = 0.001f;

        var points = Normalize(vertices);
        var normals = new List<Vector3>(sides);
        var polygons = new List<Vector3[]>(sides);

        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                for (var k = j + 1; k < points.Count; k++)
                {
                    var cross = Vector3.Cross(points[j] - points[i], points[k] - points[i]);

                    // Три точки на одной прямой плоскости не задают.
                    if (cross.Length() < MinimumArea)
                    {
                        continue;
                    }

                    var normal = Vector3.Normalize(cross);
                    var distance = Vector3.Dot(points[i], normal);

                    if (distance < 0)
                    {
                        normal = -normal;
                        distance = -distance;
                    }

                    // Плоскость через центр тела гранью быть не может.
                    if (distance <= PlaneTolerance)
                    {
                        continue;
                    }

                    // Опорная плоскость: снаружи от неё вершин нет.
                    if (points.Any(point => Vector3.Dot(point, normal) > distance + PlaneTolerance))
                    {
                        continue;
                    }

                    // Каждая грань находится столько раз, сколько в ней троек вершин.
                    if (normals.Any(found => Vector3.Dot(found, normal) > 1 - PlaneTolerance))
                    {
                        continue;
                    }

                    normals.Add(normal);
                    polygons.Add(SortAround(
                        points.Where(point =>
                            Math.Abs(Vector3.Dot(point, normal) - distance) <= PlaneTolerance).ToList(),
                        normal));
                }
            }
        }

        return new DieMesh(shape, sides, AssignValues(polygons, sides));
    }

    /// <summary>
    /// Собирает монету: два числа на плоских сторонах и гладкий ободок.
    /// </summary>
    /// <returns>Тело кубика с двумя значениями.</returns>
    private static DieMesh BuildCoin()
    {
        const int RimSegments = 28;
        const float Thickness = 0.26f;
        const float Radius = 0.97f;

        var top = new List<Vector3>(RimSegments);
        var bottom = new List<Vector3>(RimSegments);

        for (var index = 0; index < RimSegments; index++)
        {
            var angle = MathF.Tau * index / RimSegments;
            var x = Radius * MathF.Cos(angle);
            var y = Radius * MathF.Sin(angle);

            top.Add(new Vector3(x, y, Thickness));
            bottom.Add(new Vector3(x, y, -Thickness));
        }

        var faces = new List<DieFace>(RimSegments + 2)
        {
            new(top, 1),
            new(Enumerable.Reverse(bottom).ToList(), 2),
        };

        for (var index = 0; index < RimSegments; index++)
        {
            var next = (index + 1) % RimSegments;

            faces.Add(new DieFace(
                [bottom[index], bottom[next], top[next], top[index]],
                0));
        }

        return new DieMesh(DieShape.Coin, 2, Fit(faces));
    }

    /// <summary>
    /// Собирает бочонок: числа написаны на боковых гранях, торцы остаются пустыми.
    /// Такую форму имеют настоящие кубики с нечётным и необычным числом граней — d3, d5, d7.
    /// </summary>
    /// <param name="sides">Количество боковых граней.</param>
    /// <returns>Тело кубика.</returns>
    private static DieMesh BuildBarrel(int sides)
    {
        // Длина бочонка растёт с числом граней: иначе кубик с двадцатью гранями
        // превратился бы в плоскую шайбу, на которой не разглядеть чисел.
        var half = Math.Clamp(0.45f + (sides * 0.05f), 0.6f, 1.15f);
        var faces = new List<DieFace>(sides + 2);
        var front = new List<Vector3>(sides);
        var back = new List<Vector3>(sides);

        for (var index = 0; index < sides; index++)
        {
            var angle = MathF.Tau * index / sides;
            var x = MathF.Cos(angle);
            var y = MathF.Sin(angle);

            front.Add(new Vector3(x, y, half));
            back.Add(new Vector3(x, y, -half));
        }

        for (var index = 0; index < sides; index++)
        {
            var next = (index + 1) % sides;

            // Число ставится на середину боковой грани, поэтому нумерация идёт
            // по кругу и соседние значения не оказываются друг напротив друга.
            faces.Add(new DieFace(
                [back[index], back[next], front[next], front[index]],
                index + 1));
        }

        faces.Add(new DieFace(front, 0));
        faces.Add(new DieFace(Enumerable.Reverse(back).ToList(), 0));

        return new DieMesh(DieShape.Barrel, sides, Fit(faces));
    }

    /// <summary>
    /// Собирает пятиугольный трапецоэдр — привычный вид кубика d10.
    ///
    /// Высота вершины вычисляется из радиуса и высоты пояса: только при этом
    /// соотношении четыре точки каждой грани лежат в одной плоскости.
    /// </summary>
    /// <returns>Тело кубика с десятью значениями.</returns>
    private static DieMesh BuildTrapezohedron()
    {
        const int Sectors = 5;
        const float BeltHeight = 0.105f;

        var cosine = MathF.Cos(MathF.Tau / (Sectors * 2));
        var apex = BeltHeight * (1 + cosine) / (1 - cosine);

        var top = new Vector3(0, 0, apex);
        var bottom = new Vector3(0, 0, -apex);
        var upper = new Vector3[Sectors];
        var lower = new Vector3[Sectors];

        for (var index = 0; index < Sectors; index++)
        {
            var angle = MathF.Tau * index / Sectors;
            var shifted = angle + (MathF.Tau / (Sectors * 2));

            upper[index] = new Vector3(MathF.Cos(angle), MathF.Sin(angle), BeltHeight);
            lower[index] = new Vector3(MathF.Cos(shifted), MathF.Sin(shifted), -BeltHeight);
        }

        var polygons = new List<Vector3[]>(Sectors * 2);

        for (var index = 0; index < Sectors; index++)
        {
            var next = (index + 1) % Sectors;

            polygons.Add([top, upper[index], lower[index], upper[next]]);
            polygons.Add([bottom, lower[next], upper[next], lower[index]]);
        }

        var faces = AssignValues(
            polygons.Select(polygon => SortAround(polygon, Average(polygon))).ToList(),
            Sectors * 2);

        return new DieMesh(DieShape.Trapezohedron, Sectors * 2, Fit(faces));
    }

    /// <summary>
    /// Собирает шар с мелкими гранями — так выглядит настоящий стограннный кубик.
    ///
    /// Числа расставлены по граням равномерно. Если значений больше, чем граней,
    /// часть значений остаётся без собственной грани: выпавшее число в этом случае
    /// пишет представление на грани, оказавшейся перед зрителем.
    /// </summary>
    /// <param name="sides">Количество значений кубика.</param>
    /// <returns>Тело кубика.</returns>
    private static DieMesh BuildBall(int sides)
    {
        var depth = sides <= 80 ? 1 : 2;
        var triangles = Subdivide(Solids.IcosahedronTriangles(), depth);
        var labelled = Math.Min(sides, triangles.Count);
        var faces = new List<DieFace>(triangles.Count);
        var previous = 0;

        for (var index = 0; index < triangles.Count; index++)
        {
            // Числа распределяются по всей поверхности, а не сплошным пятном:
            // так шар выглядит как настоящий кубик, а не как размеченная половина.
            var next = (int)((long)(index + 1) * labelled / triangles.Count);
            var value = next > previous ? next : 0;
            previous = next;

            faces.Add(new DieFace(triangles[index], value));
        }

        return new DieMesh(DieShape.Ball, sides, faces);
    }

    /// <summary>
    /// Расставляет числа по граням так, чтобы сумма чисел противоположных граней
    /// была одинаковой — как на настоящих игральных костях.
    /// </summary>
    /// <param name="polygons">Грани тела.</param>
    /// <param name="sides">Количество значений.</param>
    /// <returns>Грани с числами.</returns>
    private static List<DieFace> AssignValues(IReadOnlyList<Vector3[]> polygons, int sides)
    {
        const float OppositeTolerance = 0.001f;

        var normals = polygons.Select(polygon => Vector3.Normalize(Average(polygon))).ToArray();
        var values = new int[polygons.Count];
        var next = 1;

        for (var index = 0; index < polygons.Count; index++)
        {
            if (values[index] != 0)
            {
                continue;
            }

            values[index] = next;

            var opposite = Array.FindIndex(
                normals,
                candidate => Vector3.Dot(candidate, -normals[index]) > 1 - OppositeTolerance);

            // У тетраэдра противоположной грани нет: числа идут подряд.
            if (opposite >= 0 && values[opposite] == 0)
            {
                values[opposite] = sides + 1 - next;
            }

            next++;
        }

        return polygons
            .Select((polygon, index) => new DieFace(polygon, values[index]))
            .ToList();
    }

    /// <summary>
    /// Упорядочивает вершины грани против часовой стрелки, если смотреть снаружи.
    /// </summary>
    /// <param name="polygon">Вершины грани в произвольном порядке.</param>
    /// <param name="normal">Направление наружу.</param>
    /// <returns>Упорядоченные вершины.</returns>
    private static Vector3[] SortAround(IReadOnlyCollection<Vector3> polygon, Vector3 normal)
    {
        var axis = Vector3.Normalize(normal);
        var first = polygon.First();
        var tangent = Vector3.Normalize(first - (axis * Vector3.Dot(first, axis)));
        var bitangent = Vector3.Cross(axis, tangent);

        return polygon
            .OrderBy(vertex => MathF.Atan2(Vector3.Dot(vertex, bitangent), Vector3.Dot(vertex, tangent)))
            .ToArray();
    }

    /// <summary>
    /// Делит каждый треугольник на четыре и выталкивает новые вершины на сферу.
    /// </summary>
    /// <param name="triangles">Исходные треугольники.</param>
    /// <param name="depth">Количество делений.</param>
    /// <returns>Треугольники поверхности шара.</returns>
    private static List<Vector3[]> Subdivide(List<Vector3[]> triangles, int depth)
    {
        for (var step = 0; step < depth; step++)
        {
            var next = new List<Vector3[]>(triangles.Count * 4);

            foreach (var triangle in triangles)
            {
                var a = triangle[0];
                var b = triangle[1];
                var c = triangle[2];
                var ab = Vector3.Normalize(a + b);
                var bc = Vector3.Normalize(b + c);
                var ca = Vector3.Normalize(c + a);

                next.Add([a, ab, ca]);
                next.Add([ab, b, bc]);
                next.Add([ca, bc, c]);
                next.Add([ab, bc, ca]);
            }

            triangles = next;
        }

        return triangles;
    }

    /// <summary>
    /// Приводит вершины к единичной сфере, сохраняя пропорции тела.
    /// </summary>
    /// <param name="vertices">Вершины тела.</param>
    /// <returns>Приведённые вершины.</returns>
    private static List<Vector3> Normalize(IReadOnlyList<Vector3> vertices)
    {
        var scale = vertices.Max(vertex => vertex.Length());

        return vertices.Select(vertex => vertex / scale).ToList();
    }

    /// <summary>
    /// Вписывает готовые грани в единичную сферу.
    /// </summary>
    /// <param name="faces">Грани тела.</param>
    /// <returns>Вписанные грани.</returns>
    private static List<DieFace> Fit(IReadOnlyList<DieFace> faces)
    {
        var scale = faces.SelectMany(face => face.Vertices).Max(vertex => vertex.Length());

        return faces
            .Select(face => new DieFace(
                face.Vertices.Select(vertex => vertex / scale).ToList(),
                face.Value))
            .ToList();
    }

    private static Vector3 Average(IReadOnlyList<Vector3> polygon)
    {
        var sum = Vector3.Zero;

        foreach (var vertex in polygon)
        {
            sum += vertex;
        }

        return sum / polygon.Count;
    }

    /// <summary>
    /// Вершины правильных многогранников.
    /// Грани по ним вычисляются, поэтому перечислять нужно только вершины.
    /// </summary>
    private static class Solids
    {
        /// <summary>Золотое сечение — основа икосаэдра и додекаэдра.</summary>
        private static readonly float Phi = (1 + MathF.Sqrt(5)) / 2;

        /// <summary>Вершины тетраэдра.</summary>
        public static readonly Vector3[] Tetrahedron =
        [
            new(1, 1, 1),
            new(1, -1, -1),
            new(-1, 1, -1),
            new(-1, -1, 1),
        ];

        /// <summary>Вершины куба.</summary>
        public static readonly Vector3[] Cube =
        [
            new(1, 1, 1), new(1, 1, -1), new(1, -1, 1), new(1, -1, -1),
            new(-1, 1, 1), new(-1, 1, -1), new(-1, -1, 1), new(-1, -1, -1),
        ];

        /// <summary>Вершины октаэдра.</summary>
        public static readonly Vector3[] Octahedron =
        [
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        ];

        /// <summary>Вершины икосаэдра.</summary>
        public static readonly Vector3[] Icosahedron =
        [
            new(0, 1, Phi), new(0, 1, -Phi), new(0, -1, Phi), new(0, -1, -Phi),
            new(1, Phi, 0), new(1, -Phi, 0), new(-1, Phi, 0), new(-1, -Phi, 0),
            new(Phi, 0, 1), new(Phi, 0, -1), new(-Phi, 0, 1), new(-Phi, 0, -1),
        ];

        /// <summary>Вершины додекаэдра.</summary>
        public static readonly Vector3[] Dodecahedron =
        [
            .. Cube,
            new(0, 1 / Phi, Phi), new(0, 1 / Phi, -Phi), new(0, -1 / Phi, Phi), new(0, -1 / Phi, -Phi),
            new(1 / Phi, Phi, 0), new(1 / Phi, -Phi, 0), new(-1 / Phi, Phi, 0), new(-1 / Phi, -Phi, 0),
            new(Phi, 0, 1 / Phi), new(Phi, 0, -1 / Phi), new(-Phi, 0, 1 / Phi), new(-Phi, 0, -1 / Phi),
        ];

        /// <summary>
        /// Возвращает треугольники икосаэдра — основу для построения шара.
        /// </summary>
        /// <returns>Двадцать треугольников с вершинами на единичной сфере.</returns>
        public static List<Vector3[]> IcosahedronTriangles()
        {
            // Ребро икосаэдра — наименьшее расстояние между его вершинами.
            // Тройка вершин, попарно соединённых рёбрами, образует грань.
            var vertices = Icosahedron.Select(Vector3.Normalize).ToArray();
            var edge = float.MaxValue;

            for (var i = 0; i < vertices.Length; i++)
            {
                for (var j = i + 1; j < vertices.Length; j++)
                {
                    edge = MathF.Min(edge, Vector3.Distance(vertices[i], vertices[j]));
                }
            }

            var limit = edge * 1.1f;
            var triangles = new List<Vector3[]>(20);

            for (var i = 0; i < vertices.Length; i++)
            {
                for (var j = i + 1; j < vertices.Length; j++)
                {
                    if (Vector3.Distance(vertices[i], vertices[j]) > limit)
                    {
                        continue;
                    }

                    for (var k = j + 1; k < vertices.Length; k++)
                    {
                        if (Vector3.Distance(vertices[i], vertices[k]) > limit
                            || Vector3.Distance(vertices[j], vertices[k]) > limit)
                        {
                            continue;
                        }

                        // Обход задаётся против часовой стрелки, если смотреть снаружи.
                        var normal = Vector3.Normalize(vertices[i] + vertices[j] + vertices[k]);
                        var winding = Vector3.Dot(
                            Vector3.Cross(vertices[j] - vertices[i], vertices[k] - vertices[i]),
                            normal);

                        triangles.Add(winding > 0
                            ? [vertices[i], vertices[j], vertices[k]]
                            : [vertices[i], vertices[k], vertices[j]]);
                    }
                }
            }

            return triangles;
        }
    }
}
