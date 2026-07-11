namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void WarmOverlayRuntimeData()
    {
        var job = JobInfo.Code(PlayerState.ClassJob.RowId);
        var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
        if (!string.IsNullOrWhiteSpace(job)
            && this.config.IconWindows.Any(window => window.Role == IconWindowRole.SkillCooldowns))
        {
            _ = this.GetJobCandidates(job, level);
        }

        if (this.config.IconWindows.Any(window => IconWindowRoles.IsStandardAuraRole(window.Role)
                                                  && window.TrackedStatusIds.Count > 0))
        {
            _ = this.EnsureStatusIdentityIndex();
        }
    }
}
