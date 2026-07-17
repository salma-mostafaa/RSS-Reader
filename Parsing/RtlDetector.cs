namespace RSS_Reader.Parsing;

public static class RtlDetector //static as never create an object
{
    //true when a majority of the letters in the text are Arabic/Hebrew-script (covers Arabic, Urdu,
    //Persian, and Hebrew, which all use scripts in these Unicode ranges). Used to decide whether the
    //frontend should render an article's title/summary with dir="rtl" regardless of the UI language.
    public static bool IsRtl(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false; //as nothing to detect

        var rtlCount = 0; //keeps track of rtl letters
        var ltrCount = 0; // ltr letters

        foreach (var c in text)
        {
            var isRtlChar =
                (c >= '\u0590' && c <= '\u05FF') || //Hebrew
                (c >= '\u0600' && c <= '\u06FF') || //Arabic (also covers Urdu/Persian, which extend Arabic script)
                (c >= '\u0750' && c <= '\u077F') || //Arabic Supplement
                (c >= '\u08A0' && c <= '\u08FF') || //Arabic Extended-A
                (c >= '\uFB50' && c <= '\uFDFF') || //Arabic Presentation Forms-A
                (c >= '\uFE70' && c <= '\uFEFF');   //Arabic Presentation Forms-B

            if (isRtlChar)
            {
                rtlCount++;
            }
            else if (char.IsLetter(c))
            {
                ltrCount++;
            }
        }

        return rtlCount > ltrCount;
    }
}
