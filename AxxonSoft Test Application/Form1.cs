using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AxxonSoft_Test_Application
{
    public partial class Form1 : Form
    {
        // Словарь: <номер треугольника, (треугольник, глубина_вложенности)>
        Dictionary<int, (Triangle, int)> trianglesDictionary = new Dictionary<int, (Triangle, int)>();
        Triangle[] triangles;
        string[] inOrOut;

        // Путь к файлу с координатами
        string filePath = @"..\..\Files\Triangles.txt";
        
        /*
         * Цвета:
         *  startColor - начальный,
         *  startColorHex - тот же цвет в HEX (для заднего фона)
         *  maxColorLevel - максимальное количество оттенков
         *  errorCondition - если есть пересекающиеся - выводим error
         *      
         */ 

        Color startColor;
        string startColorHex;
        int maxColorLevel = 0;
        bool errorCondition = false;


        public Form1()
        {
            int i = 0;
            int[] levelsOfColors;
            
            // Инициализация формы
            InitializeComponent();

            // Получаем массив треугольников из файла, ref triangles записывает все треугольники в ранее созданный массив объектов
            GetCoordinatesFromStrings(ref triangles, GetCoordinatesFromFile(filePath));
            // Проверяем все треугольники на пересечения, результаты записываются в глобальный массив inOrOut
            TriangleInOrOut();
            // Вычленяем из отрисовки пересекающиеся треугольники
            NotDrawCrossingTriangles();
            // Определяем номер оттенка для конкретного треугольника и вычисляем максимальное количество оттенков (maxColorLevel)
            levelsOfColors = MaxColorsCounter();
            
            // Далее заносим треугольники в словарь...
            foreach (var triangle in triangles)
            {
                trianglesDictionary.Add(i, (triangles[i], levelsOfColors[i]));
                i++;
            }
            // ... и отсортировываем словарь по степени вложенности, дабы избежать случаев перекрывания одниз треугольников другими
            trianglesDictionary = trianglesDictionary.OrderBy(pair => pair.Value.Item2)
                                                     .ToDictionary(pair => pair.Key, pair => pair.Value);

            // Определяем цвета для наших треугольников
            ColorPicker();

            // Посылаем запрос на отрисовку
            this.Paint += new PaintEventHandler(DrawTriangles);          
        }

        void TriangleInOrOut()
        {
            // Создаем переменную status (подробнее - в IsTriangleInTriangle) и инициализируем массив вложенностей inOrOut
            // Структура inOrOut:
            //      "T[x]" - номер треугольника (от 1),
            //      "s" (same) - тот же самый треугольник,
            //      "i" (in) - трегольник вложенный,
            //      "o" (out) - треугольник вне текущего,
            //      "c" (crossing) - треугольник пересекает текущий
            int status = 0;
            inOrOut = new string[triangles.Length];
            
            for (int i = 0; i < triangles.Length; i++)
            {
                // Добавляем номер треугольника
                inOrOut[i] = $"T[{i+1}]";

                for (int j = 0; j < triangles.Length; j++)
                {
                    // Если итераторы равны - это тот же треугольник, проверка не имеет смысла
                    if (i == j)
                    {
                        inOrOut[i] += $" s";
                        continue;
                    }

                    // Если треугольники разные - проверяем их
                    bool itit = IsTriangleInTriangle(triangles[i], triangles[j], ref status);

                    // Проверка прошла успешно и вернула статус 1 - треугольник вложен
                    if (itit && status == 1)
                    {
                        inOrOut[i] += $" i";
                    }
                    // Проверка прошла неуспешно и вернула статус -1 - треугольник находится вне текущего
                    else if (!itit && status == -1)
                    {
                        
                        inOrOut[i] += $" o";
                    }
                    // Проверка прошла неуспешно и вернула статус 0 - треугольник пересекает текущий
                    else
                    {
                        inOrOut[i] += $" c";
                        errorCondition = true;
                    }
                }
            }
        }
        
        void NotDrawCrossingTriangles()
        {
            int i = 0;
            int j = 0;
            // Создаем буферный массив, дабы была возможность изменить его без последствия для исходного массива
            string[] buff = new string[inOrOut.Length];

            // Переделываем строку: убираем все пробелы и номер треугольника в формате "T[x]", оставляем только отношения
            foreach (var x in inOrOut)
            {
                string l = x;
                l = l.Replace(" ", "");
                l = l.Substring(4);
                buff[i] = l;
                i++;
            }

            // Проверяем отношения треугольников, если у одного стоит статус "c" - изменяем drawable на false у обоих
            i = 0;
            foreach (var x in buff)
            {
                j = 0;
                foreach (var y in x)
                {
                    if (y == 'c')
                    {
                        triangles[j].drawable = false;
                        triangles[i].drawable = false;
                    }
                    j++;
                }
                i++;
            }
                
        }

        bool IsTriangleInTriangle(Triangle _t1, Triangle _t2, ref int _statuscode)
        {
            // Проверка на вложенность _t1 в _t2

            // Для удобства переписываем координаты второго треугольника в отдельные переменные
            int x1 = _t2.points[0].X;
            int y1 = _t2.points[0].Y;

            int x2 = _t2.points[1].X;
            int y2 = _t2.points[1].Y;

            int x3 = _t2.points[2].X;
            int y3 = _t2.points[2].Y;

            int i = 0;

            //Создаем массив статусов точек
            int[] pointsStatus = new int[3];

            /*
             * У точки три состояния - i(in), o(out), c(crossing)
             * 
             * Точка i если у всех выражений знаки совпадают (pointStatus = 1)
             * Точка c если хотя бы одно выражение равно 0 (pointStatus = 0)
             * Точка o в остальных случаях (pointStatus = -1)
             * 
             * Если все точки i - треугольник вписан (_statuscode = 1)
             * Если все точки o - треугольники не связаны (_statuscode = -1)
             * Если хотя бы одна точка c - треугольники пересекаются (_statuscode = 0)
             * 
             * Формулы для определения статусов точек:
             * (x1 - x0) * (y2 - y1) - (x2 - x1) * (y1 - y0)
             * (x2 - x0) * (y3 - y2) - (x3 - x2) * (y2 - y0)
             * (x3 - x0) * (y1 - y3) - (x1 - x3) * (y3 - y0)
             * 
             */

            // Массив для определения статусов точек
            foreach (var x in _t1.points)
            {
                // Для удобства подсчетов создаем массив значений формул
                double[] buff = new double[3];

                // Также создаем переменные для проверяемой точки
                int x0 = x.X;
                int y0 = x.Y;


                buff[0] = (x1 - x0) * (y2 - y1) - (x2 - x1) * (y1 - y0);
                buff[1] = (x2 - x0) * (y3 - y2) - (x3 - x2) * (y2 - y0);
                buff[2] = (x3 - x0) * (y1 - y3) - (x1 - x3) * (y3 - y0);

                /* 
                 * Блок дополнительных проверок
                 * 
                 * Данный блок необходим по той причине, что если точки коллинеарны (но не у одного треугольника),
                 * формулы выдадут 0 в одном из случаев, а полученное значение 0 соответствует пересечению.
                 * Дабы избежать такой ошибки, данный блок проверяет нахождение координаты точки между координатами
                 * точек из формул
                 * 
                 * Пример:
                 *  Есть три точки: P1(10, 50), P2(30, 50), P3(60, 50)
                 *  Первые две точки принадлежат одному треугольнику
                 *  Вторая - не принадлежит
                 *  
                 *  Одна из формул выдаст 0, поскольку все точки лежат на прямой y = 50
                 *  Однако, поскольку у третьей точки координата x явно находится дальше, чем у других,
                 *  блок проверок отнесет ее к другому треугольнику и выдаст статус -1 (out)
                 * 
                 */

                if (buff[0] == 0)
                {
                    if (x1 == x2 && x1 == x0)
                    {
                        if ((y0 <= y2 && y0 >= y1) || (y0 <= y1 && y0 >= y2))
                            buff[0] = 0;
                        else
                            buff[0] = -1;
                    }
                    else
                    {
                        if ((x0 <= x2 && x0 >= x1) || (x0 <= x1 && x0 >= x2))
                            buff[0] = 0;
                        else
                            buff[0] = -1;
                    }
                }

                if (buff[1] == 0)
                {
                    if (x3 == x2 && x3 == x0)
                    {
                        if ((y0 <= y2 && y0 >= y3) || (y0 <= y3 && y0 >= y2))
                            buff[1] = 0;
                        else
                            buff[1] = -1;
                    }
                    else
                    {
                        if ((x0 <= x2 && x0 >= x3) || (x0 <= x3 && x0 >= x2))
                            buff[1] = 0;
                        else
                            buff[1] = -1;
                    }
                }

                if (buff[2] == 0)
                {
                    if (x1 == x3 && x1 == x0)
                    {
                        if ((y0 <= y3 && y0 >= y1) || (y0 <= y1 && y0 >= y3))
                            buff[2] = 0;
                        else
                            buff[2] = -1;
                    }
                    else
                    {
                        if ((x0 <= x3 && x0 >= x1) || (x0 <= x1 && x0 >= x3))
                            buff[2] = 0;
                        else
                            buff[2] = -1;
                    }
                }

                // Если знаки в значениях одинаковы - точка лежит внутри треугольника
                if (Math.Sign(buff[0]) == Math.Sign(buff[1]) &&
                    Math.Sign(buff[0]) == Math.Sign(buff[2]))
                {
                    pointsStatus[i] = 1;
                }
                // Если при расчетах вышел 0 - точка лежит на стороне треугольника
                else if (buff[0] == 0 || buff[1] == 0 || buff[2] == 0)
                {
                    pointsStatus[i] = 0;
                }
                // В остальных случаях - точка лежит за пределами треугольника
                else
                {
                    pointsStatus[i] = -1;
                }

                i++;
            }

            // Если статус у всех точек 1 - креугольник вложен, возвращаем true и _statuscode = 1
            if (pointsStatus[0] == pointsStatus[1] && pointsStatus[0] == pointsStatus[2] && pointsStatus[0] == 1)
            {
                _statuscode = 1;
                return true;
            }

            // Если статус у всех точек -1 - креугольник находится вне другого, возвращаем false и _statuscode = -1
            if (pointsStatus[0] == pointsStatus[1] && pointsStatus[0] == pointsStatus[2] && pointsStatus[0] == -1)
            {
                _statuscode = -1;
                return false;
            }

            // В остальных случаях - треугольники пересекаются, возвращаем false и _statuscode = 0
            _statuscode = 0;
            return false;
        }

        int[] MaxColorsCounter()
        {
            int i = 0;
            int maxColors = 0;
            // Создаем массив на вывод
            int[] colorsNumsForTriangles = new int[inOrOut.Length];
            // Выбираем строку-статус у одного треугольника
            foreach (var x in inOrOut)
            {
                // Учитываем цвет заднего фона, поэтому сразу максимум ставим на 1
                int localMax = 1;
                // Проверяем все символы у строки-статуса, если нашли "i" - добавляем оттенок
                foreach (var y in x.Replace("\n", "").Split(new char[] { ' ' }))
                {
                    if (y == "i")
                        localMax++;
                }
                // Если значение оттенка вышло больше текущего максимума - перезаписываем максимум
                if (localMax > maxColors)
                    maxColors = localMax;

                // Записываем значение оттенка для определенного треугольника в массив
                colorsNumsForTriangles[i] = localMax;
                i++;
            }

            // Если флаг errorCondition был установлен в true - выводим ERROR, иначе - максимальное количество оттенков
            maxColorLevel = maxColors;
            if (errorCondition)
                label1.Text += $"\nMaxColors: ERROR";
            else
                label1.Text += $"\nMaxColors: {maxColors}";

            // Возвращаем массив оттенков
            return colorsNumsForTriangles;
        }

        void ColorPicker()
        {
            Random random = new Random();

            int i = 0;
            int colornum = 0;
            int[] color = new int[3];

            // Выбираем рандомный цвет с заданным условием (от 150 до 255). Условие такое, чтобы была возможность изменять оттенки
            while (true)
            {
                colornum = random.Next(255);
                if (colornum > 150 && colornum < 255)
                {
                    color[i] = colornum;
                    i++;
                }

                if (i == 3)
                    break;
            }
        
            // Записываем получившийся цвет в startColor и переводим его в HEX
            startColor = Color.FromArgb(color[0], color[1], color[2]);
            startColorHex = "#" + 
                            Convert.ToString(color[0], 16) + 
                            Convert.ToString(color[1], 16) + 
                            Convert.ToString(color[2], 16);

            // Выводим на экран стартовый цвет
            label1.Text += $"\nStart color:\n    RGB: ({color[0]}, {color[1]}, {color[2]})\n    HEX: {startColorHex}";
        }

        Color ColorLevel(int _level, int _maxColorLevel)
        {
            int oneLevelR = 0;
            int oneLevelG = 0;
            int oneLevelB = 0;

            if (_maxColorLevel == 1)
                _maxColorLevel = 2;

            oneLevelR = startColor.R / _maxColorLevel;
            oneLevelG = startColor.G / _maxColorLevel;
            oneLevelB = startColor.B / _maxColorLevel;

            return Color.FromArgb(startColor.R - oneLevelR * _level,
                                  startColor.G - oneLevelG * _level,
                                  startColor.B - oneLevelB * _level);
        }

        string GetCoordinatesFromFile(string _path)
        {
            //Создаем поток для чтения данных и массив байт для записи информации их файла
            FileStream fstream = new FileStream(_path, FileMode.Open);
            byte[] buffer = new byte[fstream.Length];

            //Считываем информацию и отдаем в кодировке UTF-8
            fstream.Read(buffer, 0, buffer.Length);
            string coordinatesWithNum = Encoding.UTF8.GetString(buffer);
            return coordinatesWithNum;
        }

        void GetCoordinatesFromStrings(ref Triangle[] _triangles, string _coordinatesWithNum)
        {
            // Убираем лишние '\r' символы из строки
            _coordinatesWithNum = _coordinatesWithNum.Replace("\r", "");

            // Конвертируем первую строку с числом треугольников
            // (Здесь придется применить LINQ, поскольку возникала ошибка с форматом строки,
            //  однако строка состояла из одной цифры "8" (по примеру), решить ошибку не смог, пришлось обходить) 
            string xy = _coordinatesWithNum.Split('\n')[0];
            string xyz = new string(_coordinatesWithNum.Split('\n')[0].Where(x => Char.IsDigit(x)).ToArray());
            if (xy.Contains("-"))
                xyz = xyz.Insert(0, "-");

            int trianglesNum = Convert.ToInt32(xyz);

            // Проверяем правильность заполнения данных (должен быть хотя бы один треугольник)
            if (trianglesNum < 1)
            {
                MessageBox.Show("Указано неправильное количество треугольников");
                Environment.Exit(0);
            }
            
            // Инициализируем массив треугольников
            _triangles = new Triangle[trianglesNum];

            // Парсим общую строку на подстроки координат треугольников и убираем первую строку
            string[] coordsStrs = _coordinatesWithNum.Split(new char[] { '\n' });
            DeleteElementFromStringArray(ref coordsStrs, 0);

            // Проверка на совпадение количества треугольников и количества координат
            if (trianglesNum != coordsStrs.Length)
            {
                MessageBox.Show("Количество координат не совпадает с количеством треугольников");
                Environment.Exit(0);
            }

            // Парсим координаты и добавляем в массив треугольников
            int i = 0;
            // Используем resizer, чтобы увеличить изображение, resizer = 1 - исходное изображение
            int resizer = 4;
            foreach (var coordsString in coordsStrs)
            {
                string[] coordinates = coordsString.Split(new char[] { ' ' });
                _triangles[i].points = new Point[3];

                _triangles[i].points[0].X = Convert.ToInt32(coordinates[0]) * resizer;
                _triangles[i].points[0].Y = Convert.ToInt32(coordinates[1]) * resizer;
                _triangles[i].points[1].X = Convert.ToInt32(coordinates[2]) * resizer;
                _triangles[i].points[1].Y = Convert.ToInt32(coordinates[3]) * resizer;
                _triangles[i].points[2].X = Convert.ToInt32(coordinates[4]) * resizer;
                _triangles[i].points[2].Y = Convert.ToInt32(coordinates[5]) * resizer;

                _triangles[i].center.X = (_triangles[i].points[0].X + _triangles[i].points[1].X + _triangles[i].points[2].X) / 3;
                _triangles[i].center.Y = (_triangles[i].points[0].Y + _triangles[i].points[1].Y + _triangles[i].points[2].Y) / 3;

                _triangles[i].drawable = true;

                i++;
            }
        }

        void DeleteElementFromStringArray(ref string[] _array, int _numOfElement)
        {
            if (_array.Length == 0 || _numOfElement > _array.Length - 1)
                return;

            for (int i = _numOfElement; i < _array.Length - 1; i++)
            {
                _array[i] = _array[i + 1];
            }

            Array.Resize(ref _array, _array.Length - 1);
        }

        void DrawTriangles(object sender, PaintEventArgs e)
        {
            int i = 0;
            // Создаем кисть для заливки треугольников
            SolidBrush myBrush;

            // Определяем режим сглаживания (дабы не видеть "ступенек" у линий)
            e.Graphics.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Создаем карандаш для отрисовки границ треугольников
            Pen blackPen = new Pen(Color.Black, 1);

            // Цикл отрисовки
            foreach (var triangle in trianglesDictionary)
            {
                // Если у поля drawable значение false - пропускаем отрисовку такого треугольника
                if (!triangle.Value.Item1.drawable)
                    continue;

                // Определяем кисть для заливки с посчитанным значением и параметром прозрачности alpha = 150
                myBrush = new SolidBrush(Color.FromArgb(150, ColorLevel(triangle.Value.Item2, maxColorLevel)));
                // Создаем массив точек
                PointF[] curvePoints =
                {
                     triangle.Value.Item1.points[0],
                     triangle.Value.Item1.points[1],
                     triangle.Value.Item1.points[2]
                };
                
                // Определяем цвет заднего фона
                this.BackColor = ColorTranslator.FromHtml(startColorHex);

                // Отрисовываем треугольник
                // Для просмотра номера треугольника - раскомментируйте строки DrawString
                e.Graphics.FillPolygon(myBrush, curvePoints);
                e.Graphics.DrawPolygon(blackPen, curvePoints);
                /*e.Graphics.DrawString($"{triangle.Key + 1}", 
                                      this.Font, 
                                      Brushes.White, 
                                      triangle.Value.Item1.center.X, 
                                      triangle.Value.Item1.center.Y);
                */
                i++;
            }
        }
    }

    public struct Triangle
    {
        public Point[] points;
        public Point center;
        public bool drawable;
    }
}
