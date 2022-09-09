﻿using pkuManager.WinForms.Utilities;
using System.Collections.Generic;
using System.Linq;
using System;
using pkuManager.WinForms.Formats.Fields.BAMFields;
using System.Numerics;
using pkuManager.WinForms.Formats.Fields;
using OneOf;
using pkuManager.WinForms.Formats.Fields.LambdaFields;
using pkuManager.WinForms.Formats.Modules.Tags;
using pkuManager.WinForms.Formats.Modules.MetaTags;
using pkuManager.WinForms.Formats.Modules;
using static pkuManager.Data.Dexes.SpeciesDex;
using pkuManager.Data;

namespace pkuManager.WinForms.Formats.pkx.pk3;

/// <summary>
/// An implementation of the .pk3 format used by the generation 3 GBA games.<br/>
/// Implementation details mostly referenced from
/// <see href="https://bulbapedia.bulbagarden.net/wiki/Pokémon_data_structure_(Generation_III)">Bulbapedia</see>.
/// </summary>
public class pk3Object : FormatObject, Species_O, Form_O, Shiny_O, Gender_O, Nature_O, Nickname_O,
                         Experience_O, Moves_O, PP_Ups_O, PP_O, Item_O, Ability_Slot_O, PID_O, TID_O,
                         Friendship_O, IVs_O, EVs_O, Contest_Stats_O, Ball_O, OT_O, Origin_Game_O,
                         Met_Location_O, Met_Level_O, OT_Gender_O, Language_O, Fateful_Encounter_O,
                         Markings_O, Ribbons_O, Is_Egg_O, Egg_Steps_O, Pokerus_O, ByteOverride_O
{
    public override string FormatName => "pk3";

    /* ------------------------------------
     * Initialization
     * ------------------------------------
    */
    /// <summary>
    /// The file size of a .pk3 file in the PC.
    /// </summary>
    internal const int FILE_SIZE_PC = 80;

    /// <summary>
    /// The file size of a .pk3 file in the party.
    /// </summary>
    internal const int FILE_SIZE_PARTY = 100;

    protected const bool BIG_ENDIANESS = false;
    protected const int NON_SUBDATA_SIZE = FILE_SIZE_PC - SUBDATA_SIZE;
    protected const int SUBDATA_SIZE = 4*BLOCK_SIZE;
    protected const int BLOCK_SIZE = 12;
    protected static readonly string[] SUBSTRUCTURE_ORDER =
    {
        "GAEM", "GAME", "GEAM", "GEMA", "GMAE", "GMEA",
        "AGEM", "AGME", "AEGM", "AEMG", "AMGE", "AMEG",
        "EGAM", "EGMA", "EAGM", "EAMG", "EMGA", "EMAG",
        "MGAE", "MGEA", "MAGE", "MAEG", "MEGA", "MEAG",
    };

    public ByteArrayManipulator BAM { get; } = new(FILE_SIZE_PC, BIG_ENDIANESS);

    public ByteArrayManipulator NonSubData { get; protected set; }
    public ByteArrayManipulator G { get; protected set; }
    public ByteArrayManipulator A { get; protected set; }
    public ByteArrayManipulator E { get; protected set; }
    public ByteArrayManipulator M { get; protected set; }

    // initializes blocks and fields
    public pk3Object()
    {
        // Initialize (virtual) blocks
        NonSubData = new(BAM, new[]{(0, NON_SUBDATA_SIZE)});
        G = new(BAM, new[]{(NON_SUBDATA_SIZE, BLOCK_SIZE) });
        A = new(BAM, new[]{(NON_SUBDATA_SIZE + BLOCK_SIZE, BLOCK_SIZE) });
        E = new(BAM, new[]{(NON_SUBDATA_SIZE + 2 * BLOCK_SIZE, BLOCK_SIZE) });
        M = new(BAM, new[]{(NON_SUBDATA_SIZE + 3 * BLOCK_SIZE, BLOCK_SIZE) });

        //Rearranges pk3 battle stats to match modern indices (i.e. H/A/SA/S/D/SD <-> H/A/SA/D/SD/S)
        static BigInteger[] getStats(BigInteger[] x) { DataUtil.Permutate(x, (3, 5), (3, 4)); return x; };
        static BigInteger[] setStats(BigInteger[] x) { DataUtil.Permutate(x, (3, 5), (4, 5)); return x; };

        // Non-Subdata
        PID = new(NonSubData, 0, 4);
        TID = new(NonSubData, 4, 4);
        Nickname = new(NonSubData, 8, 1, 10, this, FormatName);
        Language = new(NonSubData, 18, 1);
        Is_Bad_Egg = new(NonSubData, 19, 0);
        HasSpecies = new(NonSubData, 19, 1);
        UseEggName = new(NonSubData, 19, 2);
        Unused_A = new(NonSubData, 19, 3, 5); //leftover from egg name byte
        OT = new(NonSubData, 20, 1, 7, this, FormatName);
        Marking_Blue_Circle = new(NonSubData, 27, 0);
        Marking_Blue_Square = new(NonSubData, 27, 1);
        Marking_Blue_Triangle = new(NonSubData, 27, 2);
        Marking_Blue_Heart = new(NonSubData, 27, 3);
        Unused_B = new(NonSubData, 27, 4, 4); // leftover from markings byte
        Checksum = new(NonSubData, 28, 2);
        Unused_C = new(NonSubData, 30, 2); // probably padding

        // Block G
        Species = new(G, 0, 2);
        Item = new(G, 2, 2);
        Experience = new(G, 4, 4);
        PP_Ups = new(G, 8, 0, 2, 4);
        Friendship = new(G, 9, 1);
        Unused_D = new(G, 10, 2); // probably padding

        // Block A
        Moves = new(A, 0, 2, 4);
        PP = new(A, 8, 1, 4);

        // Block E
        EVs = new(new BAMArrayField(E, 0, 1, 6), getStats, setStats);
        Contest_Stats = new(E, 6, 1, 6);

        // Block M
        Pokerus_Days = new(M, 0, 0, 4);
        Pokerus_Strain = new(M, 0, 4, 4);
        Met_Location = new(M, 1, 1);
        Met_Level = new(M, 2, 0, 7);
        Origin_Game = new(M, 2, 7, 4);
        Ball = new(M, 2, 11, 4);
        OT_Gender = new(M, 2, 15);
        IVs = new(new BAMArrayField(M, 4, 0, 5, 6), getStats, setStats);
        Is_Egg = new(M, 4, 30);
        Ability_Slot = new(M, 4, 31);
        Cool_Ribbon_Rank = new(M, 8, 0, 3);
        Beauty_Ribbon_Rank = new(M, 8, 3, 3);
        Cute_Ribbon_Rank = new(M, 8, 6, 3);
        Smart_Ribbon_Rank = new(M, 8, 9, 3);
        Tough_Ribbon_Rank = new(M, 8, 12, 3);
        Champion_Ribbon = new(M, 8, 15);
        Winning_Ribbon = new(M, 8, 16);
        Victory_Ribbon = new(M, 8, 17);
        Artist_Ribbon = new(M, 8, 18);
        Effort_Ribbon = new(M, 8, 19);
        Battle_Champion_Ribbon = new(M, 8, 20);
        Regional_Champion_Ribbon = new(M, 8, 21);
        National_Champion_Ribbon = new(M, 8, 22);
        Country_Ribbon = new(M, 8, 23);
        National_Ribbon = new(M, 8, 24);
        Earth_Ribbon = new(M, 8, 25);
        World_Ribbon = new(M, 8, 26);
        Unused_E = new(M, 8, 27, 4); //leftover from ribbon bytes
        Fateful_Encounter = new(M, 8, 31);

        // Implicit fields
        Form = new(() => Species.Value == 201 ? TagUtil.GetPIDUnownFormID(PID.GetAs<uint>()) : 0, _ => { });
        Shiny = new(() => TagUtil.IsPIDShiny(PID.GetAs<uint>(), TID.GetAs<uint>(), Shiny_Gen6Odds), _ => { });
        Gender = new(() => {
            //deoxys, castform, and unown forms keep same gender ratio, no need to check those forms specifically.
            if (DDM.TryGetSFAMFromIDs<int?>(out SFAM sfam, FormatName, Species.GetAs<int>(), null, null, false))
                return TagEnums.Gender.Genderless; //this pk3 has an undefined SFAM
            else
            {
                if (!DDM.TryGetGenderRatio(sfam, out GenderRatio gr))
                    throw new Exception($"SPECIES '{sfam.Species}' HAS INVALID GENDERRATIO.");
                return TagUtil.GetPIDGender(gr, PID.GetAs<uint>());
            }
        }, _ => { });
        Nature = new(() => TagUtil.GetPIDNature(PID.GetAs<uint>()), _ => { });
    }


    /* ------------------------------------
     * File Conversion
     * ------------------------------------
    */
    public override byte[] ToFile()
    {
        Checksum.SetAs(CalculateChecksum()); // update checksum
        return BAM.ByteArray;
    }

    public override string TryFromFile(byte[] file)
    {
        //Must be correct size
        if (file.Length is not (FILE_SIZE_PC or FILE_SIZE_PARTY))
            return $"A .pk3 file must be {FILE_SIZE_PC} or {FILE_SIZE_PARTY} bytes long.";

        BAM.ByteArray = file;

        //No Bad Eggs
        if (IsBadEggOrInvalidChecksum)
            return "This Pokémon is a Bad Egg, and can't be imported.";

        return null; //All clear
    }

    public byte[] ToEncryptedFile()
    {
        Checksum.SetAs(CalculateChecksum()); // update checksum
        ByteArrayManipulator subData = GetEncryptedSubData(); // Encryption Step

        // PC .pk3 file is an 80 byte data structure
        ByteArrayManipulator file = new(FILE_SIZE_PC, BIG_ENDIANESS);
        file.SetArray<byte>(0, NonSubData); // First 32 bytes
        file.SetArray<byte>(NON_SUBDATA_SIZE, subData); // Last (encrypted) 48 bytes

        return file;
    }

    public void FromEncryptedFile(byte[] file)
    {
        NonSubData.SetArray(0, file, NON_SUBDATA_SIZE);
        UnencryptSubData(new ByteArrayManipulator(file[NON_SUBDATA_SIZE..FILE_SIZE_PC], BIG_ENDIANESS));
    }


    /* ------------------------------------
     * Module Parameters
     * ------------------------------------
    */
    public bool Nickname_CapitalizeDefault => true;
    public bool Shiny_Gen6Odds => false;
    public bool Shiny_PIDDependent => true;
    public bool Gender_DisallowImpossibleGenders => true;
    public bool Gender_PIDDependent => true;
    public bool Nature_PIDDependent => true;
    public bool PID_HasDependencies => true;
    public Dictionary<AbilitySlot, int> Slot_Mapping => new()
    {
        { AbilitySlot.Slot_1, 0 },
        { AbilitySlot.Slot_2, 1 },
    };
    public bool Egg_Steps_StoredInFriendship => true;


    /* ------------------------------------
     * Non-Subdata
     * ------------------------------------
    */
    public BAMIntegralField PID { get; }
    public BAMIntegralField TID { get; }
    public BAMStringField Nickname { get; }
    public BAMIntegralField Language { get; }
    public BAMBoolField Is_Bad_Egg { get; }
    public BAMBoolField HasSpecies { get; }
    public BAMBoolField UseEggName { get; }
    public BAMIntegralField Unused_A { get; }
    public BAMStringField OT { get; }
    public BAMBoolField Marking_Blue_Circle { get; }
    public BAMBoolField Marking_Blue_Square { get; }
    public BAMBoolField Marking_Blue_Triangle { get; }
    public BAMBoolField Marking_Blue_Heart { get; }
    public BAMIntegralField Unused_B { get; }
    public BAMIntegralField Checksum { get; }
    public BAMIntegralField Unused_C { get; }


    /* ------------------------------------
     * G: Growth Block
     * ------------------------------------
    */
    public BAMIntegralField Species { get; }
    public BAMIntegralField Item { get; }
    public BAMIntegralField Experience { get; }
    public BAMArrayField PP_Ups { get; }
    public BAMIntegralField Friendship { get; }
    public BAMIntegralField Unused_D { get; }


    /* ------------------------------------
     * A: Attacks Block
     * ------------------------------------
    */
    public BAMArrayField Moves { get; }
    public BAMArrayField PP { get; }


    /* ------------------------------------
     * E: EVs & Condition Block
     * ------------------------------------
    */
    public LambdaIntArrayField EVs { get; }
    public BAMArrayField Contest_Stats { get; }


    /* ------------------------------------
     * M: Misc. Block
     * ------------------------------------
    */
    public BAMIntegralField Pokerus_Days { get; }
    public BAMIntegralField Pokerus_Strain { get; }

    public BAMIntegralField Met_Location { get; }
    public BAMIntegralField Met_Level { get; }
    public BAMIntegralField Origin_Game { get; }
    public BAMIntegralField Ball { get; }
    public BAMBoolField OT_Gender { get; }

    public LambdaIntArrayField IVs { get; }
    public BAMBoolField Is_Egg { get; }
    public BAMBoolField Ability_Slot { get; }

    public BAMIntegralField Cool_Ribbon_Rank { get; }
    public BAMIntegralField Beauty_Ribbon_Rank { get; }
    public BAMIntegralField Cute_Ribbon_Rank { get; }
    public BAMIntegralField Smart_Ribbon_Rank { get; }
    public BAMIntegralField Tough_Ribbon_Rank { get; }
    public BAMBoolField Champion_Ribbon { get; }
    public BAMBoolField Winning_Ribbon { get; }
    public BAMBoolField Victory_Ribbon { get; }
    public BAMBoolField Artist_Ribbon { get; }
    public BAMBoolField Effort_Ribbon { get; }
    public BAMBoolField Battle_Champion_Ribbon { get; }
    public BAMBoolField Regional_Champion_Ribbon { get; }
    public BAMBoolField National_Champion_Ribbon { get; }
    public BAMBoolField Country_Ribbon { get; }
    public BAMBoolField National_Ribbon { get; }
    public BAMBoolField Earth_Ribbon { get; }
    public BAMBoolField World_Ribbon { get; }
    public BAMIntegralField Unused_E { get; }
    public BAMBoolField Fateful_Encounter { get; }


    /* ------------------------------------
     * Implicit Fields
     * ------------------------------------
    */
    public LambdaIntField Form { get; }
    public LambdaField<bool> Shiny { get; }
    public LambdaField<Gender> Gender { get; }
    public LambdaField<Nature> Nature { get; }


    /* ------------------------------------
     * SubData Encryption
     * ------------------------------------
    */
    /// <summary>
    /// Calculates the checksum of the 4 sub-blocks.
    /// </summary>
    protected uint CalculateChecksum()
    {
        ushort checksum = 0;
        for (int i = 0; i < SUBDATA_SIZE / 2; i++)
            checksum += BAM.Get<ushort>(NON_SUBDATA_SIZE + i * 2);
        return checksum;
    }

    protected void ApplyXOR(ByteArrayManipulator subData)
    {
        uint encryptionKey = TID.GetAs<uint>() ^ PID.GetAs<uint>();
        for (int i = 0; i < subData.Length / 4; i++) //xor subData with key in 4 byte chunks
        {
            uint chunk = subData.Get<uint>(4 * i);
            chunk ^= encryptionKey;
            subData.Set(chunk, 4 * i);
        }
    }

    /// <summary>
    /// Compiles and encrypts the current <see cref="G"/>, <see cref="A"/>, <see cref="E"/>,
    /// and <see cref="M"/> blocks with the current <see cref="PID"/> and <see cref="TID"/>.
    /// </summary>
    /// <returns>A 48 byte encrypted sub-data array.</returns>
    protected ByteArrayManipulator GetEncryptedSubData()
    {
        ByteArrayManipulator subData = new(4 * BLOCK_SIZE, BIG_ENDIANESS);
        string order = SUBSTRUCTURE_ORDER[PID.GetAs<uint>() % SUBSTRUCTURE_ORDER.Length];
        
        subData.SetArray<byte>(BLOCK_SIZE * order.IndexOf('G'), G);
        subData.SetArray<byte>(BLOCK_SIZE * order.IndexOf('A'), A);
        subData.SetArray<byte>(BLOCK_SIZE * order.IndexOf('E'), E);
        subData.SetArray<byte>(BLOCK_SIZE * order.IndexOf('M'), M);

        ApplyXOR(subData);
        return subData;
    }

    protected void UnencryptSubData(ByteArrayManipulator subData)
    {
        ApplyXOR(subData);
        string order = SUBSTRUCTURE_ORDER[PID.GetAs<uint>() % SUBSTRUCTURE_ORDER.Length];
        G.SetArray(0, subData.GetArray<byte>(BLOCK_SIZE * order.IndexOf('G'), BLOCK_SIZE));
        A.SetArray(0, subData.GetArray<byte>(BLOCK_SIZE * order.IndexOf('A'), BLOCK_SIZE));
        E.SetArray(0, subData.GetArray<byte>(BLOCK_SIZE * order.IndexOf('E'), BLOCK_SIZE));
        M.SetArray(0, subData.GetArray<byte>(BLOCK_SIZE * order.IndexOf('M'), BLOCK_SIZE));
    }


    /* ------------------------------------
     * Character Encoding
     * ------------------------------------
    */
    /// <summary>
    /// The maximum number of characters in a .pk3 nickname.<br/>
    /// Note that while the JPN games only display the first 5 of these, they are all stored under the hood.
    /// </summary>
    public const int MAX_NICKNAME_CHARS = 10;

    /// <summary>
    /// The maximum number of characters in a .pk3 OT name.
    /// </summary>
    public const int MAX_OT_CHARS = 7;


    /* ------------------------------------
     * Misc. Utility
     * ------------------------------------
    */
    /// <summary>
    /// Returns the rank of the given Gen 3 contest category.
    /// </summary>
    /// <param name="firstRibbon">The first ribbon in the contest category to check.</param>
    /// <param name="ribbons">A list of ribbons to check for the highest ranking ribbon.</param>
    /// <returns>The numeric rank of the contest category ribbon
    ///          (given by <paramref name="firstRibbon"/>) in <paramref name="firstRibbon"/>.</returns>
    public static int GetRibbonRank(Ribbon firstRibbon, HashSet<Ribbon> ribbons)
    {
        if (firstRibbon is not (Ribbon.Cool_G3 or Ribbon.Beauty_G3 or Ribbon.Cute_G3 or Ribbon.Smart_G3 or Ribbon.Tough_G3))
            throw new ArgumentException($"{nameof(firstRibbon)} must be one of the normal rank contest ribbons.", nameof(firstRibbon));

        return new[]
        {
            1 * Convert.ToInt32(ribbons.Contains(firstRibbon)),
            2 * Convert.ToInt32(ribbons.Contains(firstRibbon + 1)),
            3 * Convert.ToInt32(ribbons.Contains(firstRibbon + 2)),
            4 * Convert.ToInt32(ribbons.Contains(firstRibbon + 2)),
        }.Max();
    }

    /// <summary>
    /// Whether the given <paramref name="ribbon"/> exists in Gen 3.
    /// </summary>
    /// <param name="ribbon">The ribbon to check.</param>
    /// <returns>Whether <paramref name="ribbon"/> exists in Gen 3.</returns>
    public static bool IsValidRibbon(Ribbon ribbon) => ribbon is
        (>= Ribbon.Cool_G3 and <= Ribbon.Tough_Master_G3) or Ribbon.Champion or
        Ribbon.Winning or Ribbon.Victory or Ribbon.Artist or Ribbon.Effort or 
        Ribbon.Battle_Champion or Ribbon.Regional_Champion or Ribbon.National_Champion or 
        Ribbon.Country or Ribbon.National or Ribbon.Earth or Ribbon.World;

    public bool IsBadEggOrInvalidChecksum
        => Is_Bad_Egg.ValueAsBool || Checksum.Value != CalculateChecksum();


    /* ------------------------------------
     * Duct Tape
     * ------------------------------------
    */
    OneOf<IIntField, IField<string>> Species_O.Species => Species;
    OneOf<IIntField, IField<string>> Form_O.Form => Form;
    IField<bool> Shiny_O.Shiny => Shiny;
    OneOf<IIntField, IField<Gender>, IField<Gender?>> Gender_O.Gender => Gender;
    OneOf<IIntField, IField<Nature>> Nature_O.Nature => Nature;
    OneOf<BAMStringField, IField<string>> Nickname_O.Nickname => Nickname;
    IIntField Experience_O.Experience => Experience;
    OneOf<IIntArrayField, IField<string[]>> Moves_O.Moves => Moves;
    IIntArrayField PP_Ups_O.PP_Ups => PP_Ups;
    IIntArrayField PP_O.PP => PP;
    OneOf<IIntField, IField<string>> Item_O.Item => Item;
    IIntField Ability_Slot_O.Ability_Slot => Ability_Slot;
    IIntField Friendship_O.Friendship => Friendship;
    IIntField PID_O.PID => PID;
    IIntField TID_O.TID => TID;
    IIntArrayField IVs_O.IVs => IVs;
    IIntArrayField EVs_O.EVs => EVs;
    IIntArrayField Contest_Stats_O.Contest_Stats => Contest_Stats;
    OneOf<IIntField, IField<string>> Ball_O.Ball => Ball;
    OneOf<BAMStringField, IField<string>> OT_O.OT => OT;
    IIntField Origin_Game_O.Origin_Game => Origin_Game;
    IIntField Met_Location_O.Met_Location => Met_Location;
    IIntField Met_Level_O.Met_Level => Met_Level;
    OneOf<IIntField, IField<Gender>> OT_Gender_O.OT_Gender => OT_Gender;
    OneOf<IIntField, IField<string>> Language_O.Language => Language;
    IField<bool> Fateful_Encounter_O.Fateful_Encounter => Fateful_Encounter;
    IField<bool> Is_Egg_O.Is_Egg => Is_Egg;
    public IIntField Egg_Steps => Friendship;

    IField<bool> Markings_O.Marking_Blue_Circle => Marking_Blue_Circle;
    IField<bool> Markings_O.Marking_Blue_Square => Marking_Blue_Square;
    IField<bool> Markings_O.Marking_Blue_Triangle => Marking_Blue_Triangle;
    IField<bool> Markings_O.Marking_Blue_Heart => Marking_Blue_Heart;

    IField<bool> Ribbons_O.Champion_Ribbon => Champion_Ribbon;
    IField<bool> Ribbons_O.Winning_Ribbon => Winning_Ribbon;
    IField<bool> Ribbons_O.Victory_Ribbon => Victory_Ribbon;
    IField<bool> Ribbons_O.Artist_Ribbon => Artist_Ribbon;
    IField<bool> Ribbons_O.Effort_Ribbon => Effort_Ribbon;
    IField<bool> Ribbons_O.Battle_Champion_Ribbon => Battle_Champion_Ribbon;
    IField<bool> Ribbons_O.Regional_Champion_Ribbon => Regional_Champion_Ribbon;
    IField<bool> Ribbons_O.National_Champion_Ribbon => National_Champion_Ribbon;
    IField<bool> Ribbons_O.Country_Ribbon => Country_Ribbon;
    IField<bool> Ribbons_O.National_Ribbon => National_Ribbon;
    IField<bool> Ribbons_O.Earth_Ribbon => Earth_Ribbon;
    IField<bool> Ribbons_O.World_Ribbon => World_Ribbon;

    IIntField Pokerus_O.Pokerus_Strain => Pokerus_Strain;
    IIntField Pokerus_O.Pokerus_Days => Pokerus_Days;

}