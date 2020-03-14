using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

namespace Test_ARMO_App
{
    public partial class Form1 : Form
    {
        delegate void CurrentFileDelegate(string currentFile); //Делегат который выводит текущий обрабатываемый файл внизу экрана
        CurrentFileDelegate CFD;

        delegate void AddNodeDelegate(string currentDir, string currentFile, bool first); //Делегат добавляющий добавляет найденные файлы, а так же главную папку в TreeView
        AddNodeDelegate AND;

        delegate void SearchNodeDelegate(string currentDir, string currentPath); //Делегат позволяющий перейти к верхему узлу при возврате к верхней папке, а также добавляет папки в TreeView
        SearchNodeDelegate SND;

        delegate void EndSearchDelegate(); //Делегат заканчивающий поиск
        EndSearchDelegate ESD;

        Thread searchThread; //Объявление поискового потока

        TreeNode currentNode; //Текущий используемый узел
        string currentPath; //Текущий используемый путь

        TimeSpan t = new TimeSpan(0); //Пройденное время

        bool isSearchStopped = true; //Статус поиска

        class BufferedTreeView : TreeView //Класс который позволяет TreeView использовать DoubleBuffer
        {
            protected override void OnHandleCreated(EventArgs e)
            {
                SendMessage(this.Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
                base.OnHandleCreated(e);
            }
            private const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
            private const int TVM_GETEXTENDEDSTYLE = 0x1100 + 45;
            private const int TVS_EX_DOUBLEBUFFER = 0x0004;
            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        }

        public Form1()
        {
            InitializeComponent();
            AND = new AddNodeDelegate(AddNode);
            SND = new SearchNodeDelegate(SearchNode);
            ESD = new EndSearchDelegate(EndSearch);
            CFD = new CurrentFileDelegate(OutputCurrentFile);
        }

        private void button1_Click(object sender, EventArgs e) //Нажатие кнопки "Начать/Продолжить поиск"
        {
            if (isSearchStopped) //Если поиск не идёт
            {
                string directory = textBox1.Text;
                if (directory.Last().ToString() != "\\") //Если конце директории отсутствует "\", добавить в конец
                {
                    directory += "\\";
                }

                searchThread = new Thread(() => SearchThread(directory, textBox2.Text, textBox3.Text)); //Объявление поискового потока searchThread и перевод в него переменных пути, шаблона и текста которые нужны для поиска

                treeView1.Nodes.Clear(); //Очистка TreeView если он был заполнен ранним поиском
                t = new TimeSpan(0); //Обнуление таймера

                timer1.Start(); //Старт таймера
                toolStripLabel2.Text = "Прошло времени: 00:00:00";

                button1.Enabled = false; //Отключение и включение нужных кнопок и полей для ввода
                button2.Enabled = true;
                button3.Enabled = true;

                textBox1.Enabled = false;
                textBox2.Enabled = false;
                textBox3.Enabled = false;

                isSearchStopped = false; //Перевод в режим поиска
                button1.Text = "Продолжить поиск";

                searchThread.Start(); //Начатие поискового потока
            }
            else //Если поиск был на паузе
            {
                searchThread.Resume(); //Продолжить поток
                timer1.Start(); //Продолжить таймер

                button1.Enabled = false; //Отключение и включение нужных кнопок
                button2.Enabled = true;
            }   
        }

        private void button2_Click(object sender, EventArgs e) //Нажатие кнопки "Остановить поиск"
        {
            searchThread.Suspend(); //Приостановить поток
            timer1.Stop(); //Приостановить таймер

            button1.Enabled = true; //Отключение и включение нужных кнопок
            button2.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e) //Нажатие кнопки "Отменить поиск"
        {
            if (searchThread.ThreadState == ThreadState.Suspended) //Если поток был на паузе, продолжить его
                searchThread.Resume();
            searchThread.Abort(); //Отменить поток

            EndSearch(); //Действия связанные с остановкой поиска
        }

        private void timer1_Tick(object sender, EventArgs e) //Когда проходит 1 секунда на таймере
        {
            t = t.Add(new TimeSpan(10000000)); //Добавить секунду к прошедшему времени
            toolStripLabel2.Text = "Прошло времени: " + t.ToString("hh\\:mm\\:ss"); //Обновить текст
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) //Когда закрываем окно
        {
            if (searchThread != null) //Если поток был создан
            {
                if (searchThread.ThreadState == ThreadState.Suspended) //Если поток был на паузе, продолжить его
                    searchThread.Resume();
                searchThread.Abort(); //Отменяем поток
            }
        }

        //Процедуры связанные с потоком

        public void SearchThread(string chosenDir, string nameTemplate, string searchText) //Поток в котором происходит процедура поиска
        {
            Invoke(AND, chosenDir, Path.GetFileName(chosenDir), true); //Добавление главной папки в которой происходит поиск как главный узел в TreeView

            try
            {
                var foundFiles = Directory.EnumerateFiles(chosenDir, nameTemplate); //Нахождение всех файлов в главной папке которые соответствуют шаблону

                foreach (string currentFile in foundFiles) //Для каждого найденного файла
                {
                    Invoke(CFD, currentFile); //Указать какой файл обрабатывается
                    bool found = false;
                    foreach (var line in File.ReadAllLines(currentFile)) //Для каждой строки в файле
                    {
                        if (line.Contains(searchText)) //Если найден текст указанный пользователем, указать что найдено и отменить поиск в файле
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found) //Если текст был найден, добавить файл как узел к главному узлу
                        Invoke(AND, currentFile, Path.GetFileName(currentFile), false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при поиске файлов: " + ex.Message);
            }

            RecursiveFolderSearch(chosenDir, nameTemplate, searchText); //Рекурсивный поиск файлов во всех вложенных папках
            Invoke(ESD); //Закончить поиск
        }

        public void RecursiveFolderSearch(string currentDirectory, string nameTemplate, string searchText) //Рекурсивная процедура файлов во вложенных папках
        {
            try
            {
                var foundDirs = Directory.GetDirectories(currentDirectory);  //Нахождение всех папок в текущей директории
                foreach (string currentDir in foundDirs) //Для каждой найденной папке
                {
                    currentPath = currentDir.Substring(0, currentDir.LastIndexOf("\\") + 1); //Указываем какую директорию мы сейчас используем (удалив всё после последнего "\")
                    Invoke(SND, currentDir, currentPath); //Добавление найденной папки как узел к текущему узлу в TreeView

                    try
                    {
                        var foundFiles = Directory.EnumerateFiles(currentDir, nameTemplate); //Нахождение всех файлов в текущей папке которые соответствуют шаблону

                        foreach (string currentFile in foundFiles) //Для каждого найденного файла
                        {
                            Invoke(CFD, currentFile); //Указать какой файл обрабатывается
                            bool found = false;
                            foreach (var line in File.ReadAllLines(currentFile)) //Для каждой строки в файле
                            {
                                if (line.Contains(searchText)) //Если найден текст указанный пользователем, указать что найдено и отменить поиск в файле
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found) //Если текст был найден, добавить файл как узел к главному узлу
                                Invoke(AND, currentFile, Path.GetFileName(currentFile), false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при поиске файлов: " + ex.Message);
                    }

                    RecursiveFolderSearch(currentDir, nameTemplate, searchText); //Захождение в найденную папку через рекурсию
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при отрытии директорий: " + ex.Message);
            }
        }

        void OutputCurrentFile(string currentFile) //Вывод текущего обрабатываемого файла
        {
            toolStripLabel1.Text = "Файл: " + currentFile;
        }

        void AddNode(string currentDir, string currentFile, bool first) //Добавление файлов и главной папки как узлы в TreeView
        {
            if (first) //Если это главная папка
                currentNode = treeView1.Nodes.Add(currentDir, Path.GetFileName(currentDir.TrimEnd('\\')), 0); //Добавляем и указываем текущий используемый узел
            else //Если это файл
                currentNode.Nodes.Add(currentDir, currentFile, 1); //Добавляем как узел к текущему узлу
        }

        void SearchNode(string currentDir, string currentPath) //Переход к верхему узлу при возврате к верхней папке и добавление папки в TreeView
        {
            currentNode = treeView1.Nodes.Find(currentPath, true)[0]; //Нахождение верхнего узла по текущей используемой папке
            currentNode = currentNode.Nodes.Add(currentDir + '\\', Path.GetFileName(currentDir), 0); //Добавление папки как узел к текущему узлу в TreeView
        }

        void EndSearch() //Окончание поиска
        {
            button1.Enabled = true; //Отключение и включение нужных кнопок и полей для ввода
            button2.Enabled = false;
            button3.Enabled = false;

            textBox1.Enabled = true;
            textBox2.Enabled = true;
            textBox3.Enabled = true;

            isSearchStopped = true; //Выход из режима поиска
            button1.Text = "Начать поиск";

            toolStripLabel1.Text = "Поиск закончен";
            toolStripLabel2.Text = "Заняло времени: " + t.ToString("hh\\:mm\\:ss");
            timer1.Stop();
        }
    }
}
