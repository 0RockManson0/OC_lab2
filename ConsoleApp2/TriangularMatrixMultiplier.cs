using System;
using System.Threading;

public static class TriangularMatrixMultiplier
{
    public static Matrix MultiplySequential(TriangularMatrix a, Matrix b)
    {
        Check(a, b);
        Matrix result = new Matrix(a.Size, b.Columns);
        for (int i = 0; i < a.Size; i++) CalculateRow(a, b, result, i);
        return result;
    }

    public static Matrix MultiplyStatic(TriangularMatrix a, Matrix b, int threadCount)
    {
        Check(a, b);
        CheckThreads(threadCount);
        Matrix result = new Matrix(a.Size, b.Columns);
        Thread[] threads = new Thread[threadCount];
        int rowsPerThread = a.Size / threadCount;
        int remainder = a.Size % threadCount;

        for (int t = 0; t < threadCount; t++)
        {
            int start = t * rowsPerThread + Math.Min(t, remainder);
            int count = rowsPerThread + (t < remainder ? 1 : 0);
            int end = start + count;
            threads[t] = new Thread(() =>
            {
                for (int i = start; i < end; i++) CalculateRow(a, b, result, i);
            });
            threads[t].Start();
        }

        foreach (Thread thread in threads) thread.Join();
        return result;
    }

    public static Matrix MultiplyDynamic(TriangularMatrix a, Matrix b, int threadCount)
    {
        Check(a, b);
        CheckThreads(threadCount);
        Matrix result = new Matrix(a.Size, b.Columns);
        Thread[] threads = new Thread[threadCount];
        int nextRow = -1;

        for (int t = 0; t < threadCount; t++)
        {
            threads[t] = new Thread(() =>
            {
                while (true)
                {
                    int row = Interlocked.Increment(ref nextRow);
                    if (row >= a.Size) break;
                    CalculateRow(a, b, result, row);
                }
            });
            threads[t].Start();
        }

        foreach (Thread thread in threads) thread.Join();
        return result;
    }

    private static void CalculateRow(TriangularMatrix a, Matrix b, Matrix result, int row)
    {
        for (int j = 0; j < b.Columns; j++)
        {
            long sum = 0;
            for (int k = 0; k <= row; k++)
                sum = checked(sum + a[row, k] * b[k, j]);
            result[row, j] = sum;
        }
    }

    private static void Check(TriangularMatrix a, Matrix b)
    {
        if (a.Size != b.Rows)
            throw new ArgumentException("Количество столбцов A должно совпадать с количеством строк B.");
    }

    private static void CheckThreads(int count)
    {
        if (count < 1) throw new ArgumentException("Количество потоков должно быть положительным.");
    }
}
