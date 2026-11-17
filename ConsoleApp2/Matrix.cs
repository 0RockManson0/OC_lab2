using System;

public sealed class Matrix
{
    private readonly long[,] data;

    public int Rows { get; }
    public int Columns { get; }

    public Matrix(int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
            throw new ArgumentException("Размеры матрицы должны быть положительными.");

        Rows = rows;
        Columns = columns;
        data = new long[rows, columns];
    }

    public long this[int row, int column]
    {
        get => data[row, column];
        set => data[row, column] = value;
    }

    public static Matrix Generate(int rows, int columns, int seed = 0)
    {
        Matrix matrix = new Matrix(rows, columns);
        Random random = seed == 0 ? new Random() : new Random(seed);

        for (int i = 0; i < rows; i++)
        for (int j = 0; j < columns; j++)
            matrix[i, j] = random.Next(0, 10);

        return matrix;
    }

    public static Matrix Identity(int size)
    {
        Matrix matrix = new Matrix(size, size);
        for (int i = 0; i < size; i++) matrix[i, i] = 1;
        return matrix;
    }

    public long Checksum()
    {
        long sum = 0;
        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
            sum = checked(sum + data[i, j]);
        return sum;
    }

    // 64-битный отпечаток всех элементов: используется для проверки IPC.
    public ulong Fingerprint()
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
        {
            hash ^= unchecked((ulong)data[i, j]);
            hash = unchecked(hash * 1099511628211UL);
        }
        return hash;
    }

    public bool ContentEquals(Matrix other)
    {
        if (other == null || Rows != other.Rows || Columns != other.Columns)
            return false;

        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
            if (data[i, j] != other[i, j]) return false;

        return true;
    }

    public Matrix Copy()
    {
        Matrix copy = new Matrix(Rows, Columns);
        for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
            copy[i, j] = data[i, j];
        return copy;
    }

    public void Print()
    {
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
                Console.Write($"{this[i, j],8}");
            Console.WriteLine();
        }
    }
}
