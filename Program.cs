// See https://aka.ms/new-console-template for more information
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

//string filePath1 = @"D:\sp\CSharpDiff\file1.txt";
//string filePath2 = @"D:\sp\CSharpDiff\file2.txt";

//var result = DiffText(System.IO.File.ReadAllText(filePath1), System.IO.File.ReadAllText(filePath2), true, true, true);

//Console.WriteLine(result);

// flags from the command line
bool verboseMode = true;
bool helpMode = true;

int countPassed = 0;
int countFailed = 0;

void TestHelper(string testName, string a, string b, string expect)
{

    if (verboseMode)
    {
        Console.WriteLine($"Test2 {testName} ...");
        Console.WriteLine($"  a={a}");
        Console.WriteLine($"  b={b}");
    }

    Item[] f = DiffText(a.Replace(',', '\n'), b.Replace(',', '\n'), false, false, false);

    StringBuilder ret = new StringBuilder();
    for (int n = 0; n < f.Length; n++)
    {
        ret.Append(f[n].deletedA.ToString() + "." + f[n].insertedB.ToString() + "." + f[n].StartA.ToString() + "." + f[n].StartB.ToString() + "*");
    }

    if (verboseMode) Console.WriteLine($"  result={ret}");

    if (ret.ToString() == expect)
    {
        if (verboseMode) Console.WriteLine($"  passed");
        countPassed++;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        if (verboseMode)
        {
            Console.WriteLine($"  failed.");
        }
        else
        {
            Console.WriteLine($" {testName} failed.");
        }
        Console.ResetColor();
        countFailed++;
    }
} // TestHelper()


Console.WriteLine("Diff Self Test...");

foreach (var arg in args)
{
    Console.WriteLine($"Argument={arg}");
    if (arg == "-h") helpMode = true;
    if (arg == "--help") helpMode = true;
    if (arg == "--verbose") verboseMode = true;
    if (arg == "-v") verboseMode = true;
}

if (helpMode)
{
    Console.WriteLine("Diff [-v] [-h]");
    Console.WriteLine("  -h show this help");
    Console.WriteLine("  -v enable verbose mode");
    Console.WriteLine();
}

// test all changes
TestHelper(
  "all-changes",
  "a,b,c,d,e,f,g,h,i,j,k,l",
  "0,1,2,3,4,5,6,7,8,9",
  "12.10.0.0*");

// test all same
TestHelper(
  "all-same",
  "a,b,c,d,e,f,g,h,i,j,k,l",
  "a,b,c,d,e,f,g,h,i,j,k,l",
  "");

// test snake
TestHelper(
  "snake",
  "a,b,c,d,e,f",
  "b,c,d,e,f,x",
  "1.0.0.0*0.1.6.5*");

// 2002.09.20 - repro
TestHelper(
  "repro20020920",
  "c1,a,c2,b,c,d,e,g,h,i,j,c3,k,l",
  "C1,a,C2,b,c,d,e,I1,e,g,h,i,j,C3,k,I2,l",
  "1.1.0.0*1.1.2.2*0.2.7.7*1.1.11.13*0.1.13.15*");

// 2003.02.07 - repro
TestHelper(
  "repro20030207",
  "F",
  "0,F,1,2,3,4,5,6,7",
  "0.1.0.0*0.7.1.2*");

// Muegel - repro
TestHelper(
  "repro20030409",
  "HELLO,WORLD",
  ",,hello,,,,world,",
  "2.8.0.0*");

// test some differences
TestHelper(
  "some-changes",
  "a,b,-,c,d,e,f,f",
  "a,b,x,c,e,f",
  "1.1.2.2*1.0.4.4*1.0.7.6*");

// test one change within long chain of repeats
TestHelper(
  "long chain",
  "a,a,a,a,a,a,a,a,a,a",
  "a,a,a,a,-,a,a,a,a,a",
  "0.1.4.4*1.0.9.10*");

Console.WriteLine($"{countPassed} test passed, {countFailed} test failed");

/// <summary>
/// Find the difference in 2 text documents, comparing by textlines.
/// The algorithm itself is comparing 2 arrays of numbers so when comparing 2 text documents
/// each line is converted into a (hash) number. This hash-value is computed by storing all
/// textlines into a common hashtable so i can find duplicates in there, and generating a 
/// new number each time a new textline is inserted.
/// </summary>
/// <param name="TextA">A-version of the text (usually the old one)</param>
/// <param name="TextB">B-version of the text (usually the new one)</param>
/// <param name="trimSpace">When set to true, all leading and trailing whitespace characters are stripped out before the comparision is done.</param>
/// <param name="ignoreSpace">When set to true, all whitespace characters are converted to a single space character before the comparision is done.</param>
/// <param name="ignoreCase">When set to true, all characters are converted to their lowercase equivalence before the comparision is done.</param>
/// <returns>Returns a array of Items that describe the differences.</returns>
static Item[] DiffText(string TextA, string TextB, bool trimSpace, bool ignoreSpace, bool ignoreCase)
{
    // prepare the input-text and convert to comparable numbers.
    Hashtable h = new Hashtable(TextA.Length + TextB.Length);

    // The A-Version of the data (original data) to be compared.
    DiffData DataA = new DiffData(DiffCodes(TextA, h, trimSpace, ignoreSpace, ignoreCase));

    // The B-Version of the data (modified data) to be compared.
    DiffData DataB = new DiffData(DiffCodes(TextB, h, trimSpace, ignoreSpace, ignoreCase));

    // free up hashtable memory (maybe)
    h.Clear();

    int MAX = DataA.Length + DataB.Length + 1;
    /// vector for the (0,0) to (x,y) search
    int[] DownVector = new int[2 * MAX + 2];
    /// vector for the (u,v) to (N,M) search
    int[] UpVector = new int[2 * MAX + 2];

    LCS(DataA, 0, DataA.Length, DataB, 0, DataB.Length, DownVector, UpVector);

    Optimize(DataA);
    Optimize(DataB);
    return CreateDiffs(DataA, DataB);
} // DiffText


/// <summary>
/// If a sequence of modified lines starts with a line that contains the same content
/// as the line that appends the changes, the difference sequence is modified so that the
/// appended line and not the starting line is marked as modified.
/// This leads to more readable diff sequences when comparing text files.
/// </summary>
/// <param name="Data">A Diff data buffer containing the identified changes.</param>
static void Optimize(DiffData Data)
{
    int StartPos, EndPos;

    StartPos = 0;
    while (StartPos < Data.Length)
    {
        while ((StartPos < Data.Length) && (Data.modified[StartPos] == false))
            StartPos++;
        EndPos = StartPos;
        while ((EndPos < Data.Length) && (Data.modified[EndPos] == true))
            EndPos++;

        if ((EndPos < Data.Length) && (Data.data[StartPos] == Data.data[EndPos]))
        {
            Data.modified[StartPos] = false;
            Data.modified[EndPos] = true;
        }
        else
        {
            StartPos = EndPos;
        } // if
    } // while
} // Optimize


/// <summary>
/// Find the difference in 2 arrays of integers.
/// </summary>
/// <param name="ArrayA">A-version of the numbers (usually the old one)</param>
/// <param name="ArrayB">B-version of the numbers (usually the new one)</param>
/// <returns>Returns a array of Items that describe the differences.</returns>
static Item[] DiffInt(int[] ArrayA, int[] ArrayB)
{
    // The A-Version of the data (original data) to be compared.
    DiffData DataA = new DiffData(ArrayA);

    // The B-Version of the data (modified data) to be compared.
    DiffData DataB = new DiffData(ArrayB);

    int MAX = DataA.Length + DataB.Length + 1;
    /// vector for the (0,0) to (x,y) search
    int[] DownVector = new int[2 * MAX + 2];
    /// vector for the (u,v) to (N,M) search
    int[] UpVector = new int[2 * MAX + 2];

    LCS(DataA, 0, DataA.Length, DataB, 0, DataB.Length, DownVector, UpVector);
    return CreateDiffs(DataA, DataB);
} // Diff


/// <summary>
/// This function converts all textlines of the text into unique numbers for every unique textline
/// so further work can work only with simple numbers.
/// </summary>
/// <param name="aText">the input text</param>
/// <param name="h">This extern initialized hashtable is used for storing all ever used textlines.</param>
/// <param name="trimSpace">ignore leading and trailing space characters</param>
/// <returns>a array of integers.</returns>
static int[] DiffCodes(string aText, Hashtable h, bool trimSpace, bool ignoreSpace, bool ignoreCase)
{
    // get all codes of the text
    string[] Lines;
    int[] Codes;
    int lastUsedCode = h.Count;
    string s = "";

    // strip off all cr, only use lf as textline separator.
    aText = aText.Replace("\r", "");
    Lines = aText.Split('\n');

    Codes = new int[Lines.Length];

    for (int i = 0; i < Lines.Length; ++i)
    {
        s = Lines[i];
        if (trimSpace)
            s = s.Trim();

        if (ignoreSpace)
        {
            s = Regex.Replace(s, "\\s+", " ");            // TODO: optimization: faster blank removal.
        }

        if (ignoreCase)
        {
            s = s.ToLower();
        }

        if (!h.Contains(s))
        {
            lastUsedCode++;
            h[s] = lastUsedCode;
            Codes[i] = lastUsedCode;
        }
        else
        {
            Codes[i] = (int)(h[s]!);
        } // if
    } // for
    return (Codes);
} // DiffCodes


/// <summary>
/// This is the algorithm to find the Shortest Middle Snake (SMS).
/// </summary>
/// <param name="DataA">sequence A</param>
/// <param name="LowerA">lower bound of the actual range in DataA</param>
/// <param name="UpperA">upper bound of the actual range in DataA (exclusive)</param>
/// <param name="DataB">sequence B</param>
/// <param name="LowerB">lower bound of the actual range in DataB</param>
/// <param name="UpperB">upper bound of the actual range in DataB (exclusive)</param>
/// <param name="DownVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
/// <param name="UpVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
/// <returns>a MiddleSnakeData record containing x,y and u,v</returns>
static SMSRD SMS(DiffData DataA, int LowerA, int UpperA, DiffData DataB, int LowerB, int UpperB, int[] DownVector, int[] UpVector)
{

    SMSRD ret;
    int MAX = DataA.Length + DataB.Length + 1;

    int DownK = LowerA - LowerB; // the k-line to start the forward search
    int UpK = UpperA - UpperB; // the k-line to start the reverse search

    int Delta = (UpperA - LowerA) - (UpperB - LowerB);
    bool oddDelta = (Delta & 1) != 0;

    // The vectors in the publication accepts negative indexes. the vectors implemented here are 0-based
    // and are access using a specific offset: UpOffset UpVector and DownOffset for DownVector
    int DownOffset = MAX - DownK;
    int UpOffset = MAX - UpK;

    int MaxD = ((UpperA - LowerA + UpperB - LowerB) / 2) + 1;

    // Debug.Write(2, "SMS", String.Format("Search the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

    // init vectors
    DownVector[DownOffset + DownK + 1] = LowerA;
    UpVector[UpOffset + UpK - 1] = UpperA;

    for (int D = 0; D <= MaxD; D++)
    {

        // Extend the forward path.
        for (int k = DownK - D; k <= DownK + D; k += 2)
        {
            // Debug.Write(0, "SMS", "extend forward path " + k.ToString());

            // find the only or better starting point
            int x, y;
            if (k == DownK - D)
            {
                x = DownVector[DownOffset + k + 1]; // down
            }
            else
            {
                x = DownVector[DownOffset + k - 1] + 1; // a step to the right
                if ((k < DownK + D) && (DownVector[DownOffset + k + 1] >= x))
                    x = DownVector[DownOffset + k + 1]; // down
            }
            y = x - k;

            // find the end of the furthest reaching forward D-path in diagonal k.
            while ((x < UpperA) && (y < UpperB) && (DataA.data[x] == DataB.data[y]))
            {
                x++; y++;
            }
            DownVector[DownOffset + k] = x;

            // overlap ?
            if (oddDelta && (UpK - D < k) && (k < UpK + D))
            {
                if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                {
                    ret.x = DownVector[DownOffset + k];
                    ret.y = DownVector[DownOffset + k] - k;
                    // ret.u = UpVector[UpOffset + k];      // 2002.09.20: no need for 2 points 
                    // ret.v = UpVector[UpOffset + k] - k;
                    return (ret);
                } // if
            } // if

        } // for k

        // Extend the reverse path.
        for (int k = UpK - D; k <= UpK + D; k += 2)
        {
            // Debug.Write(0, "SMS", "extend reverse path " + k.ToString());

            // find the only or better starting point
            int x, y;
            if (k == UpK + D)
            {
                x = UpVector[UpOffset + k - 1]; // up
            }
            else
            {
                x = UpVector[UpOffset + k + 1] - 1; // left
                if ((k > UpK - D) && (UpVector[UpOffset + k - 1] < x))
                    x = UpVector[UpOffset + k - 1]; // up
            } // if
            y = x - k;

            while ((x > LowerA) && (y > LowerB) && (DataA.data[x - 1] == DataB.data[y - 1]))
            {
                x--; y--; // diagonal
            }
            UpVector[UpOffset + k] = x;

            // overlap ?
            if (!oddDelta && (DownK - D <= k) && (k <= DownK + D))
            {
                if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                {
                    ret.x = DownVector[DownOffset + k];
                    ret.y = DownVector[DownOffset + k] - k;
                    // ret.u = UpVector[UpOffset + k];     // 2002.09.20: no need for 2 points 
                    // ret.v = UpVector[UpOffset + k] - k;
                    return (ret);
                } // if
            } // if

        } // for k

    } // for D

    throw new System.ApplicationException("the algorithm should never come here.");
} // SMS


/// <summary>
/// This is the divide-and-conquer implementation of the longes common-subsequence (LCS) 
/// algorithm.
/// The published algorithm passes recursively parts of the A and B sequences.
/// To avoid copying these arrays the lower and upper bounds are passed while the sequences stay constant.
/// </summary>
/// <param name="DataA">sequence A</param>
/// <param name="LowerA">lower bound of the actual range in DataA</param>
/// <param name="UpperA">upper bound of the actual range in DataA (exclusive)</param>
/// <param name="DataB">sequence B</param>
/// <param name="LowerB">lower bound of the actual range in DataB</param>
/// <param name="UpperB">upper bound of the actual range in DataB (exclusive)</param>
/// <param name="DownVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
/// <param name="UpVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
static void LCS(DiffData DataA, int LowerA, int UpperA, DiffData DataB, int LowerB, int UpperB, int[] DownVector, int[] UpVector)
{
    // Debug.Write(2, "LCS", String.Format("Analyse the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

    // Fast walkthrough equal lines at the start
    while (LowerA < UpperA && LowerB < UpperB && DataA.data[LowerA] == DataB.data[LowerB])
    {
        LowerA++; LowerB++;
    }

    // Fast walkthrough equal lines at the end
    while (LowerA < UpperA && LowerB < UpperB && DataA.data[UpperA - 1] == DataB.data[UpperB - 1])
    {
        --UpperA; --UpperB;
    }

    if (LowerA == UpperA)
    {
        // mark as inserted lines.
        while (LowerB < UpperB)
            DataB.modified[LowerB++] = true;

    }
    else if (LowerB == UpperB)
    {
        // mark as deleted lines.
        while (LowerA < UpperA)
            DataA.modified[LowerA++] = true;

    }
    else
    {
        // Find the middle snake and length of an optimal path for A and B
        SMSRD smsrd = SMS(DataA, LowerA, UpperA, DataB, LowerB, UpperB, DownVector, UpVector);
        // Debug.Write(2, "MiddleSnakeData", String.Format("{0},{1}", smsrd.x, smsrd.y));

        // The path is from LowerX to (x,y) and (x,y) to UpperX
        LCS(DataA, LowerA, smsrd.x, DataB, LowerB, smsrd.y, DownVector, UpVector);
        LCS(DataA, smsrd.x, UpperA, DataB, smsrd.y, UpperB, DownVector, UpVector);  // 2002.09.20: no need for 2 points 
    }
} // LCS()


/// <summary>Scan the tables of which lines are inserted and deleted,
/// producing an edit script in forward order.  
/// </summary>
/// dynamic array
static Item[] CreateDiffs(DiffData DataA, DiffData DataB)
{
    ArrayList a = new ArrayList();
    Item aItem;
    Item[] result;

    int StartA, StartB;
    int LineA, LineB;

    LineA = 0;
    LineB = 0;
    while (LineA < DataA.Length || LineB < DataB.Length)
    {
        if ((LineA < DataA.Length) && (!DataA.modified[LineA])
          && (LineB < DataB.Length) && (!DataB.modified[LineB]))
        {
            // equal lines
            LineA++;
            LineB++;

        }
        else
        {
            // maybe deleted and/or inserted lines
            StartA = LineA;
            StartB = LineB;

            while (LineA < DataA.Length && (LineB >= DataB.Length || DataA.modified[LineA]))
                // while (LineA < DataA.Length && DataA.modified[LineA])
                LineA++;

            while (LineB < DataB.Length && (LineA >= DataA.Length || DataB.modified[LineB]))
                // while (LineB < DataB.Length && DataB.modified[LineB])
                LineB++;

            if ((StartA < LineA) || (StartB < LineB))
            {
                // store a new difference-item
                aItem = new Item();
                aItem.StartA = StartA;
                aItem.StartB = StartB;
                aItem.deletedA = LineA - StartA;
                aItem.insertedB = LineB - StartB;
                a.Add(aItem);
            } // if
        } // if
    } // while

    result = new Item[a.Count];
    a.CopyTo(result);

    return (result);
}

/// <summary>details of one difference.</summary>
public struct Item
{
    /// <summary>Start Line number in Data A.</summary>
    public int StartA;
    /// <summary>Start Line number in Data B.</summary>
    public int StartB;

    /// <summary>Number of changes in Data A.</summary>
    public int deletedA;
    /// <summary>Number of changes in Data B.</summary>
    public int insertedB;
} // Item

/// <summary>
/// Shortest Middle Snake Return Data
/// </summary>
struct SMSRD
{
    internal int x, y;
    // internal int u, v;  // 2002.09.20: no need for 2 points 
}

/// <summary>Data on one input file being compared.  
/// </summary>
internal class DiffData
{

    /// <summary>Number of elements (lines).</summary>
    internal int Length;

    /// <summary>Buffer of numbers that will be compared.</summary>
    internal int[] data;

    /// <summary>
    /// Array of booleans that flag for modified data.
    /// This is the result of the diff.
    /// This means deletedA in the first Data or inserted in the second Data.
    /// </summary>
    internal bool[] modified;

    /// <summary>
    /// Initialize the Diff-Data buffer.
    /// </summary>
    /// <param name="data">reference to the buffer</param>
    internal DiffData(int[] initData)
    {
        data = initData;
        Length = initData.Length;
        modified = new bool[Length + 2];
    } // DiffData
} // DiffData

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