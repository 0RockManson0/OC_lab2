using System;

// Нижнетреугольная матрица хранит только элементы j <= i.
public sealed class TriangularMatrix
{
    private readonly long[][] data;
    public int Size { get; }

    public TriangularMatrix(int size)
    {
        if (size <= 0) throw new ArgumentException("Размер должен быть положительным.");
        Size = size;
        data = new long[size][];
        for (int i = 0; i < size; i++) data[i] = new long[i + 1];
    }

    public long this[int row, int column]
    {
        get
        {
            if (row < 0 || row >= Size || column < 0 || column >= Size)
                throw new IndexOutOfRangeException();
            return column > row ? 0 : data[row][column];
        }
        set
        {
            if (row < 0 || row >= Size || column < 0 || column >= Size)
                throw new IndexOutOfRangeException();
            if (column > row)
            {
                if (value != 0)
                    throw new ArgumentException("Элементы выше диагонали не хранятся и должны быть равны нулю.");
                return;
            }
            data[row][column] = value;
        }
    }

    public static TriangularMatrix Generate(int size, int seed = 0)
    {
        TriangularMatrix matrix = new TriangularMatrix(size);
        Random random = seed == 0 ? new Random() : new Random(seed);
        for (int i = 0; i < size; i++)
        for (int j = 0; j <= i; j++)
            matrix[i, j] = random.Next(0, 10);
        return matrix;
    }

    public long Checksum()
    {
        long sum = 0;
        for (int i = 0; i < Size; i++)
        for (int j = 0; j <= i; j++)
            sum = checked(sum + data[i][j]);
        return sum;
    }
}
