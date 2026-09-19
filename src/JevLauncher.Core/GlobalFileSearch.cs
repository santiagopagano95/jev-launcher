using System.Data.OleDb;
using System.IO;

namespace JevLauncher.Core;

/// <summary>Global file search through the Windows Search index (OLE DB). Best effort.</summary>
public static class GlobalFileSearch
{
    public static Task<IReadOnlyList<Candidate>> SearchAsync(string query, int max = 20, CancellationToken ct = default) =>
        Task.Run(() => Search(query, max), ct);

    public static IReadOnlyList<Candidate> Search(string query, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<Candidate>();

        try
        {
            using var connection = new OleDbConnection(
                "Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\";");
            connection.Open();

            using var command = new OleDbCommand(BuildSql(Sanitize(query), Math.Clamp(max, 1, 50)), connection);
            using var reader = command.ExecuteReader();

            var list = new List<Candidate>();
            while (reader.Read())
            {
                var path = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(path)) continue;

                var name = reader.IsDBNull(1) ? Path.GetFileName(path) : reader.GetString(1);
                var modified = reader.IsDBNull(2) ? DateTime.Now : Convert.ToDateTime(reader.GetValue(2));
                list.Add(LocalIndex.FileCandidateFromPath(path, modified));
            }

            return list;
        }
        catch
        {
            // The index can be disabled or the provider missing; callers fall back to folder search.
            return Array.Empty<Candidate>();
        }
    }

    public static string Sanitize(string query) => query.Replace("'", "''").Trim();

    public static string BuildSql(string query, int max) =>
        $"SELECT TOP {max} System.ItemPathDisplay, System.ItemNameDisplay, System.DateModified " +
        $"FROM SystemIndex WHERE \"System.FileName\" LIKE '%{query}%' AND \"System.ItemType\" <> 'Directory'";
}
