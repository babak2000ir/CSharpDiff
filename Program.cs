// See https://aka.ms/new-console-template for more information
string filePath1 = @"D:\sp\CSharpDiff\file1.txt";
string filePath2 = @"D:\sp\CSharpDiff\file2.txt";

diff_match_patch dmp = new diff_match_patch();
List<Diff> diffs = dmp.diff_main(System.IO.File.ReadAllText(filePath1), System.IO.File.ReadAllText(filePath2));

foreach (Diff diff in diffs)
{
    Console.WriteLine(diff.ToString());
}

//var result = DiffText(System.IO.File.ReadAllText(filePath1), System.IO.File.ReadAllText(filePath2), true, true, true);



/* use this to mka eit more efficient:
def myers_diff_length_half_memory(old, new):
    N = len(old)
    M = len(new)
    MAX = N + M

    V = [None] * (MAX + 2)
    V[1] = 0
    for D in range(0, MAX + 1):
        for k in range(-(D - 2*max(0, D-M)), D - 2*max(0, D-N) + 1, 2):
            if k == -D or k != D and V[k - 1] < V[k + 1]:
                x = V[k + 1]
            else:
                x = V[k - 1] + 1
            y = x - k
            while x < N and y < M and old[x] == new[y]:
                x = x + 1
                y = y + 1
            V[k] = x
            if x == N and y == M:
                return D */