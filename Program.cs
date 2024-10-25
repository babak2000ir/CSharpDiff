// See https://aka.ms/new-console-template for more information
string filePath1 = @"D:\sp\CSharpDiff\file1.txt";
string filePath2 = @"D:\sp\CSharpDiff\file2.txt";

string[] file1Lines = File.ReadAllLines(filePath1);
string[] file2Lines = File.ReadAllLines(filePath2);

List<string> diff = Diff(file1Lines, file2Lines);

foreach (string line in diff)
{
    Console.WriteLine(line);
}

static List<string> Diff(string[] oldStr, string[] newStr)
{
    int N = oldStr.Length;
    int M = newStr.Length;
    int MAX = N + M;

    Dictionary<int, int> V = new Dictionary<int, int>();
    V[1] = 0;

    for (int D = 0; D <= MAX; D++)
    {
        for (int k = -(D - 2 * Math.Max(0, D - M)); k <= D - 2 * Math.Max(0, D - N); k += 2)
        {
            int x;
            if (k == -D || (k != D && V[k - 1] < V[k + 1]))
                x = V[k + 1];
            else
                x = V[k - 1] + 1;

            int y = x - k;

            while (x < N && y < M && oldStr[x] == newStr[y])
            {
                x++;
                y++;
            }

            V[k] = x;

            if (x == N && y == M)
            {
                return Backtrack(oldStr, newStr, N, M, V);
            }
        }
    }

    return new List<string>();
}


static List<string> Backtrack(string[] file1, string[] file2, int N, int M, Dictionary<int, int> v)
{
    List<string> result = new List<string>();
    int x = N;
    int y = M;

    while (x > 0 || y > 0)
    {
        if (y > 0 && (x == 0 || v[x - 1] < v[x]))
        {
            result.Add($"Add {file2[y - 1]}");
            y--;
        }
        else if (x > 0 && (y == 0 || v[x - 1] >= v[x]))
        {
            result.Add($"Delete {file1[x - 1]}");
            x--;
        }
        else
        {
            result.Add($"Keep {file1[x - 1]}");
            x--;
            y--;
        }
    }
    result.Reverse();
    return result;
}
