namespace YazioTool.Core.Domain;

/// <summary>Energy in kcal, macronutrients in grams.</summary>
public readonly record struct Nutrition(double Kcal, double Protein, double Fat, double Carbs)
{
    public static Nutrition Zero => default;

    public bool IsNonNegative => Kcal >= 0 && Protein >= 0 && Fat >= 0 && Carbs >= 0;

    public Nutrition Scale(double factor) =>
        new(Kcal * factor, Protein * factor, Fat * factor, Carbs * factor);

    public static Nutrition operator +(Nutrition a, Nutrition b) =>
        new(a.Kcal + b.Kcal, a.Protein + b.Protein, a.Fat + b.Fat, a.Carbs + b.Carbs);

    public static Nutrition operator -(Nutrition a, Nutrition b) =>
        new(a.Kcal - b.Kcal, a.Protein - b.Protein, a.Fat - b.Fat, a.Carbs - b.Carbs);
}
