using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Lets administrators manage fitness programmes (FR-22): add one, rename it, change which plan level unlocks it, or hide it.
/// Hiding keeps the lessons and customers' progress; the programme and its lessons simply stop appearing to customers.
/// </summary>
public class ProgrammeAdminService(AppDbContext db, ScheduleAdminService? schedule = null)
{
    /// <summary>
    /// Deletes a programme with all its lessons, the progress people made on them, and any uploaded video files. Unlike hiding, this cannot be undone.
    /// </summary>
    public async Task<(bool Ok, bool NotFound)> DeleteAsync(int id)
    {
        var p = await db.Programmes.FindAsync(id);
        if (p is null) return (false, true);
        var lessonIds = await db.Lessons.Where(l => l.ProgrammeId == id).Select(l => l.Id).ToListAsync();
        foreach (var lid in lessonIds)
        {
            if (schedule is not null) await schedule.DeleteLessonAsync(lid);
            else
            {
                db.Progress.RemoveRange(db.Progress.Where(x => x.LessonId == lid));
                db.Lessons.Remove((await db.Lessons.FindAsync(lid))!);
                await db.SaveChangesAsync();
            }
        }
        db.Programmes.Remove(p);
        await db.SaveChangesAsync();
        return (true, false);
    }

    public async Task<List<AdminProgrammeDto>> ListAsync()
    {
        var rows = await db.Programmes.AsNoTracking().Include(p => p.Lessons).OrderBy(p => p.Id).ToListAsync();
        return rows.Select(p => new AdminProgrammeDto(p.Id, p.Name, p.Description, p.MinTier, p.Status == "ACTIVE", p.Lessons.Count)).ToList();
    }

    public async Task<(bool Ok, int Id, string? Error)> CreateAsync(ProgrammeUpsert r)
    {
        var name = r.Name.Trim();
        if (name.Length == 0) return (false, 0, "Give the programme a name.");
        if (await NameTakenAsync(name, exceptId: null)) return (false, 0, "There is already a programme with that name.");

        var p = new FitnessProgramme
        {
            Name = name, Description = r.Description?.Trim() ?? "", MinTier = r.MinTier, Status = r.Active ? "ACTIVE" : "INACTIVE",
        };
        db.Programmes.Add(p);
        await db.SaveChangesAsync();
        return (true, p.Id, null);
    }

    public async Task<(bool Ok, bool NotFound, string? Error)> UpdateAsync(int id, ProgrammeUpsert r)
    {
        var p = await db.Programmes.FindAsync(id);
        if (p is null) return (false, true, null);
        var name = r.Name.Trim();
        if (name.Length == 0) return (false, false, "Give the programme a name.");
        if (await NameTakenAsync(name, exceptId: id)) return (false, false, "There is already a programme with that name.");

        p.Name = name;
        p.Description = r.Description?.Trim() ?? "";
        p.MinTier = r.MinTier;
        p.Status = r.Active ? "ACTIVE" : "INACTIVE";
        await db.SaveChangesAsync();
        return (true, false, null);
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId)
    {
        var lower = name.ToLower();
        return db.Programmes.AnyAsync(p => p.Name.ToLower() == lower && (exceptId == null || p.Id != exceptId));
    }
}
