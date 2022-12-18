using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace OpenGL_lesson_CSharp
{
    internal class ValueSave
    {
        private string dirrectory = Environment.CurrentDirectory + "/";

        public void SetSaveValue(string index, string value)
        {
            if (File.Exists(dirrectory + index + ".ini"))
            {
                Console.WriteLine("По данному индексу ранее уже сохранялось значение. \nВы хотете его перезаписать? [Yeas - y : No - n]");
                loop:
                string answer = Console.ReadLine();
                switch (answer)
                {
                    case "y":
                        File.WriteAllText(dirrectory + index + ".ini", value);
                        break;
                    case "n":
                        Console.WriteLine("Значение не будет перезаписано.");
                        break;
                    default:
                        Console.WriteLine("Ответ не верный! Регистр имеет значение.");
                        goto loop;
                }
            }
            else
            {
                File.WriteAllText(dirrectory + index + ".ini", value);
                Console.WriteLine("Значение успешно записано!");
            }
        }
        public string GetSaveValue(string index)
        {
            if (File.Exists(dirrectory + index + ".ini"))
            {
                StreamReader sr = new StreamReader(dirrectory + index + ".ini");
                string text = sr.ReadToEnd();
                sr.Close();
                return text;
            }
            else
            {
                Console.WriteLine("По индексу {0} не существует сохраненных значений", index);
                return null;
            }
        }
        public void DeleteAll()
        {
            string[] _base = Directory.GetFiles(Environment.CurrentDirectory, "*.ini");
            foreach (string item in _base)
            {
                File.Delete(item);
            }
        }
    }
}
