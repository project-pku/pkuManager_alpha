﻿using pkuManager.Formats.Fields;
using pkuManager.Formats.pkx;
using static pkuManager.Formats.PorterDirective;

namespace pkuManager.Formats.Modules;

public interface Contest_Stats_O
{
    public IntegralArrayField Contest_Stats { get; }
}

public interface Contest_Stats_E : MultiNumericTag
{
    public Contest_Stats_O Data { get; }

    public int Contest_Stats_Default => 0;
    public bool Contest_Stats_SilentUnspecified => true;

    [PorterDirective(ProcessingPhase.FirstPass)]
    public void ProcessContest_Stats()
        => ProcessMultiNumericTag("Contest Stats", pkxUtil.STAT_NAMES, pku.Contest_Stats_Array, Data.Contest_Stats,
                                  Contest_Stats_Default, Contest_Stats_SilentUnspecified);
}