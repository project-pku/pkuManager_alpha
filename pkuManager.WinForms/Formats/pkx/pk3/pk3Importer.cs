using pkuManager.WinForms.Alerts;
using pkuManager.WinForms.Formats.Modules;
using pkuManager.WinForms.Formats.Modules.MetaTags;
using pkuManager.WinForms.Formats.Modules.Tags;
using pkuManager.WinForms.Formats.Modules.Templates;
using pkuManager.WinForms.Formats.pku;
using System;
using static pkuManager.WinForms.Alerts.Alert;
using static pkuManager.WinForms.Formats.PorterDirective;

namespace pkuManager.WinForms.Formats.pkx.pk3;

public class pk3Importer : Importer, SFA_I, Gender_I, Shiny_I, Nature_I,
                           Is_Egg_I, Language_I, Nickname_I, OT_I
{
    public override string FormatName => "pk3";

    public override pk3Object Data { get; } = new();

    public pk3Importer(byte[] file, GlobalFlags globalFlags, bool checkInMode) : base(file, globalFlags, checkInMode)
    {
        Reason = Data.TryFromFile(file);
    }


    /* ------------------------------------
     * Working Variables
     * ------------------------------------
    */
    protected bool legalGen3Egg;
    public ChoiceAlert Language_DependencyError { get; set; }


    /* ------------------------------------
     * Custom Import Methods
     * ------------------------------------
    */
    // Egg
    public void ImportIs_Egg()
    {
        (this as Is_Egg_I).ImportIs_EggBase();

        //deal with legal gen 3 egg
        if (Data.Is_Egg.ValueAsBool)
        {
            (string str, bool inv) = StringTagUtil.Decode(Data.Nickname.Value, FormatName, "Japanese");
            legalGen3Egg = Data.UseEggName.ValueAsBool //egg name override set
                && Data.Language.Value == 1 //language set to japanese (index = 1)
                && !inv && str == TagUtil.EGG_NICKNAME["Japanese"]; //nickname set to "タマゴ"
        }

        //byte override use egg name, if not handled by 'legalGen3Egg' thing
        if (!legalGen3Egg && Data.UseEggName.ValueAsBool)
            Warnings.Add(ByteOverrideUtil.AddByteOverrideCMD("Egg Name Flag", Data.UseEggName.GetOverride(), pku, FormatName));
    }

    // Language
    [PorterDirective(ProcessingPhase.FirstPass, nameof(ImportIs_Egg))]
    public void ImportLanguage()
    {
        if (legalGen3Egg) //make language invalid to set off lang dep error
            Data.Language.Value = 0; //set language to none (index = 0)

        (this as Language_I).ImportLanguageBase();

        if (legalGen3Egg) //return legal egg lang back to japanese
            Data.Language.Value = 1; //set language to japanese (index = 1)
    }

    // Nickname
    public void ImportNickname()
    {
        if (legalGen3Egg)
            pku.Nickname.Value = null;
        else
            (this as Nickname_I).ImportNicknameBase();
    }


    /* ------------------------------------
     * Error Resolvers
     * ------------------------------------
    */
    public ErrorResolver<string> Language_Resolver { get; set; }
    public Action Nickname_Resolver { get; set; }
    public ErrorResolver<string> OT_Resolver { get; set; }


    /* ------------------------------------
     * Custom Alerts
     * ------------------------------------
    */
    public ChoiceAlert GetLanguageDependencyAlert(AlertType at, string[] langs, int defChoice)
    {
        ChoiceAlert a = (this as Language_I).GetLanguageDependencyAlertBase(at, langs, defChoice);
        a.Message = "Some text values in this format require a language, but Gen 3 eggs don't have a set language. Please choose one:";
        return a;
    }
}