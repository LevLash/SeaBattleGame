using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SeaBattleGame
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpListener listener;
        TcpClient client;
        StreamReader reader;
        StreamWriter writer;
        List<Button> myButtons;
        List<Button> enemyButtons;
        Task receiveTask;
        CancellationTokenSource source;
        int[,] fieldMatrix;
        int[,] enemyFieldMatrix;
        int[] ships = new int[] { 4, 3, 2, 1 };
        int[] step = new int[] { 0, 0 };

        public MainWindow()
        {
            InitializeComponent();
            myButtons = new List<Button>();
            for (int i = 0; i < 100; i++)
            {
                myButtons.Add(new Button());
            }
            enemyButtons = new List<Button>();
            for (int i = 0; i < 100; i++)
            {
                enemyButtons.Add(new Button());
            }

            fieldMatrix = new int[myButtons.Count / 10, myButtons.Count / 10];
            enemyFieldMatrix = new int[enemyButtons.Count / 10, enemyButtons.Count / 10];

            setButtons(ref myButtons, 6, 2);
            setButtons(ref enemyButtons, 20, 2);

            foreach (var item in myButtons)
            {
                App.Current.Dispatcher.Invoke(() => { item.Click += PrepareButton_Click; });
            }
            foreach (var item in enemyButtons)
            {
                App.Current.Dispatcher.Invoke(() => { item.Click += GameButton_Click; });
            }

            PrepareButtonsEnable(false);
            clearButtons();
        }

        private void setButtons(ref List<Button> buttons, int i, int j)
        {
            int maxField = i + 10;
            foreach (var item in buttons)
            {
                field.Children.Add(item);
                Grid.SetColumn(item, i);
                Grid.SetRow(item, j);

                i++;

                if (i >= maxField)
                {
                    i = maxField - 10;
                    j += 1;
                }

                item.IsEnabled = false;
            }
        }

        //-----------------------------------------------------------------------------------Connection part
        private async void MenuItemCreate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CreateServerDialog();
            var result = dlg.ShowDialog();
            if (result == null || result.Value == false)
            {
                return;
            }
            btnCreate.IsEnabled = false;
            btnConnect.IsEnabled = false;
            var localEP = new IPEndPoint(dlg.IPAddress, dlg.Port);
            listener = new TcpListener(localEP);
            listener.Start();
            client = await listener.AcceptTcpClientAsync();
            CreateReceiveTask();
            listener.Stop();
        }

        private async void MenuItemConnect_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CreateServerDialog() { Title = "Connect to server" };
            var result = dlg.ShowDialog();
            if (result == null || result.Value == false)
            {
                return;
            }
            var tcpClient = new TcpClient(AddressFamily.InterNetwork);
            await tcpClient.ConnectAsync(dlg.IPAddress, dlg.Port);
            if (tcpClient.Connected)
            {
                client = tcpClient;
                CreateReceiveTask();
            }
            btnCreate.IsEnabled = false;
            btnConnect.IsEnabled = false;
        }

        private void MenuItemDisconnect_Click(object sender, RoutedEventArgs e)
        {
            client.Close();
            btnCreate.IsEnabled = true;
            btnConnect.IsEnabled = true;
        }

        //----------------------------------------------------------------------------Receive part

        private void Received(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var buffer = new byte[1024];
                try
                {
                    var amount = client.Client.Receive(buffer);
                    var s = Encoding.UTF8.GetString(buffer, 0, amount);
                    var ss = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var pt = (PackageType)Convert.ToInt32(ss[0]);
                    switch (pt)
                    {
                        case PackageType.Start:
                            PrepareButtonsEnable(true);
                            EnableButtons(ref myButtons, true);
                            Dispatcher.Invoke(() => { btnStart.IsEnabled = false; });
                            Dispatcher.Invoke(() => { btn_readyPrepare.IsEnabled = false; });
                            break;
                        case PackageType.Ready:
                            foreach (Button item in myButtons)
                            {
                                Dispatcher.Invoke(() => {
                                    if (item.Content != null)
                                    {
                                        if (item.Content.ToString() == "x")
                                        {
                                            item.Content = null;
                                        }
                                    }
                                });
                            }
                            EnableButtons(ref myButtons, false);
                        EnableButtons(ref enemyButtons, ss[1] == "1");
                            PrepareButtonsEnable(false);
                            break;
                        case PackageType.Turn:
                            var i = Convert.ToInt32(ss[1]);
                            var j = Convert.ToInt32(ss[2]);
                            if (fieldMatrix[i, j] == 0)
                            {
                                fieldMatrix[i, j] = 2;
                            }
                            else if (fieldMatrix[i, j] == 1)
                            {
                                fieldMatrix[i, j] = 3;
                            }
                        Dispatcher.Invoke(() => { myButtons[i + j * 10].Content = CheckField(fieldMatrix[i, j]); });
                            break;
                        case PackageType.Result:
                            enemyFieldMatrix[Convert.ToInt32(ss[1]), Convert.ToInt32(ss[2])] = 1;
                            break;
                        case PackageType.Check:
                            {
                                var gr = (GameResult)Convert.ToInt32(ss[1]);
                                if (gr == GameResult.lose)
                                {
                                    MessageBox.Show("You lose!!");
                                    clearButtons();
                                    return;
                                }
                            }
                            break;
                        case PackageType.Next:
                            EnableButtons(ref enemyButtons, true);
                            break;
                        default:
                            break;
                    }
                }
                catch (SocketException) { }
            }
        }

        private void CreateReceiveTask()
        {
            var ns = client.GetStream();
            reader = new StreamReader(ns);
            writer = new StreamWriter(ns);
            source = new CancellationTokenSource();
            writer.AutoFlush = true;
            receiveTask = Task.Run(() => { Received(source.Token); });
        }

        //----------------------------------------------------------------------------Start part
        private async void MenuItemStart_Click(object sender, RoutedEventArgs e)
        {
            var p = $"{Convert.ToInt32(PackageType.Start)};";
            await writer.WriteAsync(p);
            EnableButtons(ref myButtons, true);
            PrepareButtonsEnable(true);
            Dispatcher.Invoke(() => { btn_readyPrepare.IsEnabled = false; });
        }

        //--------------------------------------------------------------------------------------------Prepare Part
        private async void PrepareButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Content != null)
            {
                return;
            }

            var index = myButtons.IndexOf(btn);
            int i = index % 10;
            int j = index / 10;
            if (ship1_rbtn.IsChecked == true)
            {
                if (ship1_text.Text[4].ToString() == "0")
                {
                    return;
                }

                btn.Content = "[]";
                fieldMatrix[i, j] = 1;

                blockAround(index);

                ship1_text.Text = $"[] ({--ships[0]})";
            }
            else if(ship2_rbtn.IsChecked == true)
            {
                if (ship2_text.Text[6].ToString() == "0")
                {
                    return;
                }
                if (step[0] == 0)
                {
                    btn.Content = "[]";
                    fieldMatrix[i, j] = 1;
                    EnableButtons(ref myButtons, false, true);

                    radioButtonEnable(false);
                    nextStep(index);

                    step[1] = index;
                    step[0]++;
                }
                else if (step[0] == 1)
                {
                    btn.Content = "[]";
                    fieldMatrix[i, j] = 1;
                    blockAround(index);
                    blockAround(index - (index - step[1]));

                    radioButtonEnable(true);
                    step[0] = 0;
                    step[1] = 0;
                    EnableButtons(ref myButtons, true, true);
                    ship2_text.Text = $"[][] ({--ships[1]})";
                }

            }
            else if(ship3_rbtn.IsChecked == true)
            {
                if (ship3_text.Text[8].ToString() == "0")
                {
                    return;
                }
                if (step[0] == 0)
                {
                    btn.Content = "[]";
                    fieldMatrix[i, j] = 1;
                    EnableButtons(ref myButtons, false, true);

                    radioButtonEnable(false);
                    nextStep(index);

                    step[1] = index;
                    step[0]++;
                }
                else if (step[0] == 1)
                {
                    int nextIndex = index + (index - step[1]);
                    if (nextIndex < 0 || nextIndex > myButtons.Count || (nextIndex % 10 == 0 && index % 10 == 9) || (nextIndex % 10 == 9 && index % 10 == 0))
                    {
                        return;
                    }
                    if (myButtons[nextIndex].Content != null)
                    {
                        return;
                    }
                    btn.Content = "[]";
                    myButtons[nextIndex].Content = "[]";
                    fieldMatrix[i, j] = 1;
                    fieldMatrix[nextIndex % 10, nextIndex / 10] = 1;

                    EnableButtons(ref myButtons, false, true);
                    blockAround(nextIndex);
                    blockAround(index);
                    blockAround(step[1]);
                    radioButtonEnable(true);
                    step[0] = 0;
                    step[1] = 0;
                    EnableButtons(ref myButtons, true, true);
                    var p2 = $"{Convert.ToInt32(PackageType.Result)};{nextIndex % 10};{nextIndex / 10}";
                    await writer.WriteAsync(p2);
                    ship3_text.Text = $"[][][] ({--ships[2]})";
                }
            }
            else if(ship4_rbtn.IsChecked == true)
            {
                if (ship4_text.Text[10].ToString() == "0")
                {
                    return;
                }
                if (step[0] == 0)
                {
                    btn.Content = "[]";
                    fieldMatrix[index % 10, index / 10] = 1;
                    EnableButtons(ref myButtons, false, true);

                    radioButtonEnable(false);
                    nextStep(index);

                    step[1] = index;
                    step[0]++;
                }
                else if (step[0] == 1)
                {
                    int nextIndex = index + (index - step[1]);
                    int index4 = nextIndex + (nextIndex - index);
                    if (index4 < 0 || index4 > myButtons.Count || 
                        (index4 % 10 == 0 && nextIndex % 10 == 9) || (index4 % 10 == 9 && nextIndex % 10 == 0) || 
                        (nextIndex % 10 == 0 && index % 10 == 9) || (nextIndex % 10 == 9 && index % 10 == 0))
                    {
                        return;
                    }
                    if (myButtons[index4].Content != null)
                    {
                        return;
                    }
                    btn.Content = "[]";
                    myButtons[nextIndex].Content = "[]";
                    myButtons[index4].Content = "[]";
                    fieldMatrix[index % 10, index / 10] = 1;
                    fieldMatrix[nextIndex % 10, nextIndex / 10] = 1;
                    fieldMatrix[index4 % 10, index4 / 10] = 1;
                    var p2 = $"{Convert.ToInt32(PackageType.Result)};{nextIndex % 10};{nextIndex / 10}";
                    await writer.WriteAsync(p2);
                    var p3 = $"{Convert.ToInt32(PackageType.Result)};{index4 % 10};{index4 / 10}";
                    await writer.WriteAsync(p3);


                    EnableButtons(ref myButtons, false, true);
                    blockAround(nextIndex);
                    blockAround(index4);
                    blockAround(index);
                    blockAround(step[1]);
                    radioButtonEnable(true);
                    step[0] = 0;
                    step[1] = 0;
                    EnableButtons(ref myButtons, true, true);
                    ship4_text.Text = $"[][][][] ({--ships[3]})";
                }
            }
            else
            {
                return;
            }
            var p = $"{Convert.ToInt32(PackageType.Result)};{i};{j}";
            await writer.WriteAsync(p);
            if (ships[0] <= 0 && ships[1] <= 0 && ships[2] <= 0 && ships[3] <= 0)
            {
                btn_readyPrepare.IsEnabled = true;
            }
        }

        private void radioButtonEnable(bool value)
        {
            ship1_rbtn.IsEnabled = value;
            ship2_rbtn.IsEnabled = value;
            ship3_rbtn.IsEnabled = value;
            ship4_rbtn.IsEnabled = value;
        }

        private void nextStep(int index)
        {
            if (index % 10 != 9)
            {
                myButtons[index + 1].IsEnabled = true;
            }

            if (index / 10 != 9)
            {
                myButtons[index + 10].IsEnabled = true;
            }

            if (index % 10 != 0)
            {
                myButtons[index - 1].IsEnabled = true;
            }

            if (index / 10 != 0)
            {
                myButtons[index - 10].IsEnabled = true;
            }
        }

        private void blockAround(int index)
        {
            for (int i = index % 10 != 0 ? index % 10 - 1 : index % 10; i <= (index % 10 != 9 ? index % 10 + 1 : index % 10); i++)
            {
                for (int j = index / 10 != 0 ? index / 10 - 1 : index / 10; j <= (index / 10 != 9 ? index / 10 + 1 : index / 10); j++)
                {
                    if (myButtons[i + j * 10].Content == null)
                    {
                        myButtons[i + j * 10].Content = "x";
                    }
                }
            }
        }

        private async void btn_readyPrepare_Click(object sender, RoutedEventArgs e)
        {
            int cnt = 0;
            for (int i = 0; i < enemyFieldMatrix.Length / 10; i++)
            {
                for (int j = 0; j < enemyFieldMatrix.Length / 10; j++)
                {
                    if (enemyFieldMatrix[i,j] == 1)
                    {
                        cnt++;
                    }
                }
            }
            if (cnt < 20)
            {
                MessageBox.Show("Wait for your partner");
                return;
            }
            foreach (Button item in myButtons)
            {
                if (item.Content != null)
                {
                    if (item.Content.ToString() == "x")
                    {
                        item.Content = null;
                    }
                }
            }
            var rnd = new Random((int)DateTime.Now.Ticks);
            var start = rnd.Next() % 2;
            var p = $"{Convert.ToInt32(PackageType.Ready)};{start}";
            await writer.WriteAsync(p);

            EnableButtons(ref enemyButtons, start == 0);
            EnableButtons(ref myButtons, false);
            PrepareButtonsEnable(false);

        }

        //--------------------------------------------------------------------------Game part

        private async void GameButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var index = enemyButtons.IndexOf(btn);
            int i = index % 10;
            int j = index / 10;
            var p = $"{Convert.ToInt32(PackageType.Turn)};{i};{j};";
            await writer.WriteAsync(p);
            if (enemyFieldMatrix[i, j] == 0)
            {
                enemyFieldMatrix[i, j] = 2;
            }
            else if (enemyFieldMatrix[i, j] == 1)
            {
                enemyFieldMatrix[i, j] = 3;
            }
            btn.Content = CheckField(enemyFieldMatrix[i, j]);

            var gr = checkGame();
            p = $"{Convert.ToInt32(PackageType.Check)};{Convert.ToInt32(gr == GameResult.win ? GameResult.lose : gr)};";
            await writer.WriteAsync(p);
            if (gr == GameResult.win)
            {
                MessageBox.Show("You win!!!");
                return;
            }

            p = $"{Convert.ToInt32(PackageType.Next)};";
            await writer.WriteAsync(p);
            EnableButtons(ref enemyButtons, false, true);
        }

        private GameResult checkGame()
        {
            int cnt = 0;
            for (int i = 0; i < enemyFieldMatrix.Length / 10; i++)
            {
                for (int j = 0; j < enemyFieldMatrix.Length / 10; j++)
                {
                    if (enemyFieldMatrix[i,j] == 1)
                    {
                        return GameResult.none;
                    }
                }
            }
            return  GameResult.win;
        }

        private void PrepareButtonsEnable(bool enable)
        {
            Dispatcher.Invoke(() =>
            {
                ships_panel.IsEnabled = enable;
                ships_panel.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
                btn_readyPrepare.IsEnabled = enable;
                btn_readyPrepare.Visibility = enable ? Visibility.Visible : Visibility.Hidden;

            });
        }

        //----------------------------------------------------------------------------Quit part
        private void MenuItemQuit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //----------------------------------------------------------------------------Button help part

        private void clearButtons()
        {
            for (int i = 0; i < fieldMatrix.Length / 10; i++)
            {
                for (int j = 0; j < fieldMatrix.Length / 10; j++)
                {
                    fieldMatrix[i,j] = 0;
                    enemyFieldMatrix[i, j] = 0;
                }
            }

            foreach (var item in myButtons)
            {
                App.Current.Dispatcher.Invoke(() => {
                    item.Content = null;
                });
            }
            foreach (var item in enemyButtons)
            {
                App.Current.Dispatcher.Invoke(() => {
                    item.Content = null;
                });
            }
        }

        private void EnableButtons(ref List<Button> buttons, bool enable, bool disableUsed = false)
        {
            foreach (var item in buttons)
            {
                Dispatcher.Invoke(() =>
                {
                    if (item.Content == null || disableUsed)
                    {
                        item.IsEnabled = enable;
                    }
                });
            }
        }

        //----------------------------------------------------------------------------Change field part

        private string CheckField(int value)
        {
            switch (value)
            {
                case 0:
                    return null;
                case 1:
                    return "[]";
                case 2:
                    return "#";
                case 3:
                    return "@";
                default:
                    return null;
            }
        }
    }
}
