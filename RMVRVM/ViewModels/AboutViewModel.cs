using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.PlatformConfiguration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace RMVRVM.ViewModels
{
    public class TaskComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            int id1 = Convert.ToInt32(x.Split(' ')[1]);
            int id2 = Convert.ToInt32(y.Split(' ')[1]);
            if (id1 > id2) return -1;
            if (id1 < id2) return 1;
            return 0;
        }
    }
    public class TaskStatus {
        public string TaskId { get; set; }
        public string Status { get; set; }
    }
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel()
        {
            Title = "Experiment";
            StartCommand = new Command(() => StartSimulation());
            StopCommand = new Command(() => StopSimulation());
            RefreshTaskStatus = new Command(() => UpdateTaskStatus());
            Duration = BatteryStart = BatteryCurrent = Consumption = Rate = "---";
        }

        private void StopSimulation()
        {
            if (UseRmvrvm)
            {
                StopRemoteTasks();
            }
            Task.Run(() => {
                SaveReport();
                EnableStartButton = true;
            });
        }

        private void StopRemoteTasks()
        {
            HttpClient httpClient = new HttpClient();
            var uri = new Uri(baseUrl + stopExpUrl);
            httpClient.GetAsync(uri);
        }
        private async Task GetRemoteTasksStatus()
        {
            HttpClient httpClient = new HttpClient();
            var uri = new Uri(baseUrl + taskStatusUrl);
            lock (remoteSyncObj)
            {
                remoteTaskStatusReqPending = true;
            }
            var resp = await httpClient.GetAsync(uri);
            lock (remoteSyncObj)
            {
                remoteTaskStatusReqPending = false;
            }
            var ts = JArray.Parse(resp.Content.ReadAsStringAsync().Result);
            taskStatusVM.Clear();
            ts.ForEach(t => taskStatusVM.Add(new TaskStatus { TaskId = t["TaskId"].ToString(), Status = t["Status"].ToString() }));
            TaskStatus = taskStatusVM;
        }
        private void StartRemoteTasks()
        {
            HttpClient httpClient = new HttpClient();
            var uri = new Uri(baseUrl + startExpUrl + Convert.ToString(Iterations));
            httpClient.GetAsync(uri);
        }

        private void SaveReport()
        {
            HttpClient httpClient = new HttpClient();
            var uri = new Uri(baseUrl + consumptionReportUrl);
            var content = new StringContent(JsonConvert.SerializeObject(consumptionReport), Encoding.UTF8, "application/json");
            httpClient.PostAsync(uri, content);
        }

        bool remoteTaskStatusReqPending;
        object remoteSyncObj = new object();

        const string stopExpUrl = "stopexperiment";
        const string taskStatusUrl = "taskstatus";
        const string startExpUrl = "startexperiment?iterations=";
        const string consumptionReportUrl = "uploadconsumptioneport";
        const string rateReportUrl = "uploadratereport";
        const string baseUrl = "https://rmvrvmbackend20210913222459.azurewebsites.net/api/CPUIntensiveTasks/";
        private readonly SortedDictionary<string, TaskStatus> taskStatusDictionary = new SortedDictionary<string, TaskStatus>(new TaskComparer());
        private readonly List<Task> tasks = new List<Task>();
        readonly object syncObj = new object();
        private ObservableCollection<TaskStatus> taskStatusVM = new ObservableCollection<TaskStatus>();
        private readonly List<Tuple<string, string>> rateReport = new List<Tuple<string, string>>();
        private readonly List<Tuple<string, string>> consumptionReport = new List<Tuple<string, string>>();
        private double lastConsumption = 0;
        private void UpdateTaskStatus()
        {
            lock (syncObj)
            {
                if (!UseRmvrvm)
                    TakeLast50Tasks();
                else if (!remoteTaskStatusReqPending)
                    _ = GetRemoteTasksStatus();

                SetConsumptionReport();
            }
        }
        private void SetConsumptionReport()
        {
            var curConsumption = Convert.ToDouble(Consumption.Replace("%", string.Empty)); 
            if (lastConsumption != curConsumption)
            {
                lastConsumption = curConsumption;
                consumptionReport.Add(new Tuple<string, string>(Duration, curConsumption.ToString("F")));
            }
        }
        private void TakeLast50Tasks()
        {
            taskStatusDictionary.Clear();
            for (int i = tasks.Count - 1; i >= tasks.Count - 50 && i >= 0; i--)
            {
                taskStatusDictionary["Task " + i] = new TaskStatus { TaskId = "Task " + i, Status = tasks[i].IsCompleted ? "Completed" : "Running" };
            }
            taskStatusVM.Clear();
            taskStatusDictionary.Values.ForEach(val => taskStatusVM.Add(val));
            TaskStatus = taskStatusVM;
        }
        private void SetRateReport()
        {
            var con = Convert.ToDouble(Consumption.Replace("%", string.Empty));
            var dur = TimeSpan.ParseExact(Duration, @"hh\:mm\:ss", CultureInfo.InvariantCulture);
            var curRate = con / dur.TotalHours;
            rateReport.Add(new Tuple<string, string>(Duration, curRate.ToString("F")));
            Rate = "( " + curRate.ToString("F") + "% per hour )";
        }
        private void StartSimulation()
        {
            SetDefaultState();
            if (UseRmvrvm)
            {
                StartRemoteTasks();
            }
            Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                var initCharge = Battery.ChargeLevel * 100.0;
                BatteryStart = Convert.ToString(initCharge) + "%";
                for (int i = 0; i < int.MaxValue; i++)
                {
                    var charge = Battery.ChargeLevel * 100.0;

                    if (!UseRmvrvm)
                        tasks.Add(Task.Run(() => CPUIntesiveTask(Iterations)));

                    Thread.Sleep(1000);
                    Duration = (DateTime.Now - start).ToString(@"hh\:mm\:ss");
                    BatteryCurrent = Convert.ToString(charge) + "%";
                    Consumption = (initCharge - charge).ToString("F") + "%";
                    if (EnableStartButton)
                        break;
                    UpdateTaskStatus();
                }
            });
        }
        private void SetDefaultState()
        {
            EnableStartButton = false;
            tasks.Clear();
            taskStatusDictionary.Clear();
            taskStatusVM.Clear();
            TaskStatus = taskStatusVM;
            Rate = "( 0.00 % per hour )";
            rateReport.Clear();
            consumptionReport.Clear();
            if (UseRmvrvm)
                consumptionReport.Add(new Tuple<string, string>("Mode","rMVrVM"));
            else
                consumptionReport.Add(new Tuple<string, string>("Mode","normal"));
            lastConsumption = 0.0;
        }

        private static void CPUIntesiveTask(int itr)
        {
            for (int j = 0; j < int.MaxValue/itr; j++)
            {
                Random r = new Random();
                var t = r.NextDouble();
                var art = Math.Tan(t);
                Math.Atan(art);
                Math.Atan2(t, art);
                Math.Atan2(art, t);
            }
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RefreshTaskStatus { get; }

        private string duration;
        public string Duration { get { return duration; } set => SetProperty(ref duration, value); }
        private string batteryStart;
        public string BatteryStart { get { return batteryStart; } set => SetProperty(ref batteryStart, value); }
        private string batteryCurrent;
        public string BatteryCurrent { get => batteryCurrent; set => SetProperty(ref batteryCurrent, value); }
        private string consumption;
        public string Consumption { get { return consumption; } set => SetProperty(ref consumption, value); }
        private string rate;
        public string Rate { get => rate; set => SetProperty(ref rate, value); }
        private bool stop = true;
        public bool EnableStartButton { get => stop; set => SetProperty(ref stop, value); }
        public ObservableCollection<TaskStatus> TaskStatus { get { return taskStatusVM; } set => SetProperty(ref taskStatusVM, value); }
        private int iterations = 10000;
        public int Iterations { get => iterations; set => SetProperty(ref iterations, value); }
        private bool useRmvrvm = true;
        public bool UseRmvrvm { get => useRmvrvm; set => SetProperty(ref useRmvrvm, value); }
    }
}