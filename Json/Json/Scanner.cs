using System;
using System.Collections.Generic;
using System.Text;

namespace Json
{
    public static class Scanner
    {
        public static void MovePastNextIgnoringWhitespace(string json, ref int index, char characterToStopAt)
        {
            while (index < json.Length)
            {
                var character = json[index++];

                if (character == characterToStopAt)
                {
                    return;
                }
                else
                {
                    EnsureWhitespace(character);
                }
            }
        }

        private static bool IsCharacterThatCanAppearInNumber(char character)
        {
            if (character >= '0' && character <= '9')
            {
            	return true;
            }

            switch (character)
            {
                case 'e':
                case 'E':
                case '+':
                case '-':
                case '.':
                    return true;
                default:
                    return false;
            }
        }
        public static void ScanPastDouble(string json, ref int index, out int startingIndex, out int length)
        {
            startingIndex = index;
            length = 0;

            while (index < json.Length)
            {
                var character = json[index];

                if (!IsCharacterThatCanAppearInNumber(character))
                {
                    length = index - startingIndex;
                    return;
                }

                ++index;
            }
        }

        public static void GetNextString(string json, ref int index, out Substring substring)
        {
            if (json[index] != '\"')
            {
                throw new Exception("No string here...");
            }

            index++;
            int startingIndex = index;

            while (index < json.Length)
            {
                var character = json[index];
                if (character == '\\')
                {
                    BuildNextStringFromEscaped(json, ref index, startingIndex, out substring);
                    return;
                }
                else if (character == '\"')
                {
                    int stringLength = index - startingIndex;
                    substring = new Substring(json, startingIndex, stringLength);
                    index++;
                    return;
                }
                else
                {
                    index++;
                }
            }

            throw new Exception("No ending quote to string.");
        }

        private static void BuildNextStringFromEscaped(string json, ref int index, int startingIndex, out Substring substring)
        {
            int lengthFromStartToFirstBackslash = index - startingIndex;
            var stringBuilder = new StringBuilder(json, startingIndex, lengthFromStartToFirstBackslash, (int)(1.6f * lengthFromStartToFirstBackslash)); ;
            
            while (index < json.Length)
            {
                var character = json[index];
                if (character == '\\')
                {
                    var indexOfBackslashForEscaping = index;
                    index++;
                    character = json[index];

                    switch (character)
                    {
                        case '\'':
                        case '\"':
                        case '\\':
                        case '/':
                            stringBuilder.Append(character);
                            index++;
                            break;
                        case 'n':
                            stringBuilder.Append('\n');
                            index++;
                            break;
                        case 'r':
                            stringBuilder.Append('\r');
                            index++;
                            break;
                        case 'f':
                            stringBuilder.Append('\f');
                            index++;
                            break;
                        case 'b':
                            stringBuilder.Append('\b');
                            index++;
                            break;
                        case 't':
                            stringBuilder.Append('\t');                    
                            index++;
                            break;
                        case 'u':
                            {
                                const int numberOfHexCharacterForJsonEncodedUtfCharacter = 4;
                                
                                index++;

                                var hexValue = HexSerializer.Parse(json, ref index, numberOfHexCharacterForJsonEncodedUtfCharacter);

                                var unicodeCharacter = (char)hexValue;
                                stringBuilder.Append(unicodeCharacter);
                            }
                            break;

                        default:
                            throw new Exception("Invalid escape character");
                    }
                }
                else if (character == '\"')
                {
                    string jsonWithEscapedCharactersRemoved = stringBuilder.ToString();
                    substring = new Substring(jsonWithEscapedCharactersRemoved);
                    
                    index++;
                    
                    return;
                }
                else
                {
                    stringBuilder.Append(character);
                    index++;
                }
            }

            throw new Exception("No ending quote to string.");
        }

        private static void EnsureWhitespace(char character)
        {
            switch (character)
            {
                case ' ':
                case '\n':
                case '\r':
                    break;
                default:
                    throw new Exception("Invalid character");
            }
        }

        private static void SkipToEndOfBalancedBlock(string json, ref int index, char startCharacter, char endCharactar)
        {
            int count = 0;
            while (index < json.Length)
            {
                var character = json[index];
                if (character == '\"')
                {
                    SkipString(json, ref index);
                }
                else
                {
                    if (character == endCharactar)
                    {
                        count--;
                        if (count == 0)
                        {
                            index++;
                            return;
                        }
                    }
                    else if (character == startCharacter)
                    {
                        count++;
                    }
                    index++;
                }
            }
        }

        public static void SkipNextValue(string json, ref int index)
        {
            while (index < json.Length)
            {
                var character = json[index];

                switch (character)
                {
                    case '\"':
                        SkipString(json, ref index);
                        return;
                    case '[':
                        SkipToEndOfBalancedBlock(json, ref index, '[', ']');
                        return;
                    case '{':
                        SkipToEndOfBalancedBlock(json, ref index, '{', '}');
                        return;
                    case 't':
                        if (!SkipWord(json, ref index, "true"))
                        {
                            throw new Exception("Invalid");
                        }
                        return;
                    case 'f':
                        if (!SkipWord(json, ref index, "false"))
                        {
                            throw new Exception("Invalid");
                        }
                        return;
                    case 'n':
                        if (!SkipWord(json, ref index, "null"))
                        {
                            throw new Exception("Invalid"); 
                        }
                        return;
                    case ' ':
                    case '\n':
                    case '\r':
                        break;
                    default:
                        if (!IsCharacterThatCanAppearInNumber(character))
                        {
                            return;
                        }
                        break;
                }
                ++index;
            }
        }

        public static bool SkipWord(string json, ref int index, string word)
        {
            if (string.Compare(json, index, word, 0, word.Length) == 0)
            {
                index += word.Length;
                return true;
            }

            return false;
        }

        private static void SkipString(string json, ref int index)
        {
            if (json[index] != '\"')
            {
                throw new Exception("No string here...");
            }

            index++;

            while (index < json.Length)
            {
                var character = json[index];
                if (character == '\\')
                {
                    index += 2;
                }
                else if (character == '\"')
                {
                    index++;
                    return;
                }
                else
                {
                    index++;
                }
            }

            throw new Exception("No ending quote to string.");
        }

        public static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                var character = json[index];

                switch (character)
                {
                    case ' ':
                    case '\n':
                    case '\r':
                        break;
                    default:
                        return;
                }
                ++index;
            }
        }
    }
}
