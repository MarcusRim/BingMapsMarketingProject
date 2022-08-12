/*
 * Copyright(c) 2017 Microsoft Corporation. All rights reserved. 
 * 
 * This code is licensed under the MIT License (MIT). 
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is furnished to do 
 * so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software. 
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE. 
*/

using BingMapsRESTToolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RESTToolkitTestApp
{
    public partial class MainWindow : Window
    {
        #region Private Properties

        private string BingMapsKey = System.Configuration.ConfigurationManager.AppSettings.Get("BingMapsKey");

        private DispatcherTimer _timer;
        private TimeSpan _time;

        #endregion
        int mode = -1; // 1 for Geocode, 2 for LocationRecog

        //Default parameters
        double coord1 = 0.0;
        double coord2 = 0.0;
        String geocodeAddress = "545 W Lambert Rd, Brea, CA 92821";
        static int NumResults = 10;
        static double SearchRadius = 0.15;

        object[,] PushpinsData = new object[NumResults, 3];
        AddressInfo GlobalAddressInfo = new AddressInfo();
        String SelectedCompanyName = "";
        ListBoxItem prev = new ListBoxItem();

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            //import addresses from Properties table and puts them in the listbox
            using (var db = new Model1())
            {
                int i = 0;
                var query = from b in db.Properties
                            orderby b.ID
                            select b;
                foreach (var item in query)
                {
                    var listboxitem = new ListBoxItem { Content = item.Address_Full };
                    listboxitem.Selected += Address_Clicked;
                    listboxitem.Tag = new AddressInfo(item.ID, item.Nickname, item.Address_Full, item.Units, item.RegionID, item.Notes, item.RecordSource);
                    if (i % 10 == 0)
                        listboxitem.Background = Brushes.Bisque;
                    
                    //GreenYellow is if the property has been meddled with, but none of the company statuses has been set
                    //LimeGreen(darker green) is if the property has at least one status set in the database
                    if (item.Lattitude != null)
                    {
                        listboxitem.Background = Brushes.GreenYellow;
                        var statuscheck = db.PropertyTenants.Where(c => c.PropertyID == item.ID).OrderByDescending(d => d.Updated).FirstOrDefault();
                        if (statuscheck != null)
                            if (statuscheck.ClientStatus != null || statuscheck.TargetType != null || item.PropertyStatus != null)
                                listboxitem.Background = Brushes.LimeGreen;
                    }
                    AddressListBox.Items.Add(listboxitem);
                    i++;
                }
                GetStatusLists();
            }
            _timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                if (_time != null)
                {
                    RequestProgressBarText.Text = string.Format("Time remaining: {0}", _time);

                    if (_time == TimeSpan.Zero)
                    {
                        _timer.Stop();
                    }

                    _time = _time.Add(TimeSpan.FromSeconds(-1));
                }
            }, Application.Current.Dispatcher);
            //Sends data to the map
            wb.LoadCompleted += WebBrowser_LoadCompleted;
            wb.LoadCompleted += SendPushpinData;
        }

        #endregion

        #region Methods

        //Get status options
        private void GetStatusLists()
        {
            using (var db = new Model1())
            {
                var query1 = from psc in db.D1PropertyStatusCodes
                            orderby psc.CodeID
                            select psc;
                foreach (var item in query1)
                {
                    var menuitem = new MenuItem { Header = item.Description, Tag = "psc" };
                    menuitem.Click += Status_Clicked;
                    PropertyStatusMenu.Items.Add(menuitem);
                }

                var query2 = from cs in db.D1ClientStatusCodes
                            orderby cs.CodeID
                            select cs;

                foreach (var item in query2)
                {
                    var menuitem = new MenuItem { Header = item.Description, Tag = "cs" };
                    menuitem.Click += Status_Clicked;
                    ClientStatusMenu.Items.Add(menuitem);
                }

                var query3 = from tt in db.D1TargetTypes
                             orderby tt.TypeID
                             select tt;

                foreach (var item in query3)
                {
                    var menuitem = new MenuItem { Header = item.Description, Tag = "tt" };
                    menuitem.Click += Status_Clicked;
                    TargetTypeMenu.Items.Add(menuitem);
                }
            }
        }

        //Displays chosen status
        private void Status_Clicked(object sender, RoutedEventArgs e)
        {
            var tempobj = (MenuItem)sender;
            if (tempobj.Tag.ToString() == "psc")
                PropertyStatusMenu.Header = tempobj.Header;
            else if (tempobj.Tag.ToString() == "cs")
                ClientStatusMenu.Header = tempobj.Header;
            else if (tempobj.Tag.ToString() == "tt")
                TargetTypeMenu.Header = tempobj.Header;
        }

        private void GeocodeBtn_Clicked(object sender, RoutedEventArgs e)
        {
            var r = new GeocodeRequest()
            {
                Query = geocodeAddress,
                IncludeIso2 = true,
                IncludeNeighborhood = true,
                MaxResults = 25,
                BingMapsKey = BingMapsKey
            };
            mode = 1;
            ProcessRequest(r, sender, e);
            GeocodeCompleted += LocationRecogBtn_Clicked;
        }

        private void LocationRecogBtn_Clicked(object sender, RoutedEventArgs e)
        {
            PushpinsData = new object[NumResults, 3];
            var r = new LocationRecogRequest()
            {
                BingMapsKey = BingMapsKey,
                CenterPoint = new Coordinate(coord1, coord2),
                Top = NumResults,
                Radius = SearchRadius
            };
            mode = 2;
            ProcessRequest(r, sender, e);
        }

        //sends coordinates of center and radius of search to the html file
        private void WebBrowser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            wb.InvokeScript("SetCoords", new object[] { coord1, coord2, SearchRadius });
        }

        //sends the pin data to the html file
        private void SendPushpinData(object sender, NavigationEventArgs e)
        {
            ArrayList addresslist = new ArrayList();
            for (int i = 0; i < PushpinsData.GetLength(0); i++)
            {
                addresslist.Add(PushpinsData[i, 0]);
            }
            for (int i = 0; i < PushpinsData.GetLength(0); i++)
            {
                int duplicate = 1; //serves as boolean
                if (addresslist.IndexOf(PushpinsData[i, 0]) == addresslist.LastIndexOf(PushpinsData[i, 0]))
                    duplicate = 0;
                if(PushpinsData[i,0]!=null)
                    wb.InvokeScript("GetDataOfLocation", new object[] { PushpinsData[i, 0], PushpinsData[i, 1], PushpinsData[i, 2], duplicate });
            }
        }

        //Custom event to chain Geocode Request and Location Recog Request
        public delegate void Complete(object sender, RoutedEventArgs e);

        public event Complete GeocodeCompleted;

        //Displays html file map in the UI
        private void OpenMap_Clicked(object sender, RoutedEventArgs e)
        {
            string path = (new System.Uri(Assembly.GetExecutingAssembly().Location)).AbsolutePath;
            path = path.Substring(0, path.IndexOf("bin")) + "index.htm";
            wb.Navigate(path);
            NewMapButton.IsEnabled = true;
        }

        //Takes user to Google Map for better streetview
        private void GoogleMap_Clicked(object sender, RoutedEventArgs e)
        {
            String mapurl = "https://www.google.com/maps/search/?api=1&query=" + geocodeAddress;
            System.Diagnostics.Process.Start(mapurl);
        }

        //Recenters the search to the purple pin
        private void NewLocationRecog_Clicked(object sender, RoutedEventArgs e)
        {
            coord1 = Convert.ToDouble(wb.InvokeScript("GetLat"));
            coord2 = Convert.ToDouble(wb.InvokeScript("GetLong"));
            LocationRecogBtn_Clicked(sender, e);
            OpenMap_Clicked(sender, e);
        }

        //Alternative to presing "Go" only for the address textbox "GeocodeAddressTbx"
        private void EnterPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Go_Clicked(sender, e);
            }
        }

        //Displays all companies from database that contain the letters in the search box. Pressing Enter doesn't work for this textbox
        private void UpdateCompanyList(object sender, RoutedEventArgs e)
        {
            var button = (Button) sender;
            button.IsEnabled = false;
            CompanyListBox.Items.Clear();
            NoResults.Text = "";
            if (CompanySearchBar.Text.Length <2)
                CompanySearchBar.Text = "More than 1 letter";
            else
            {
                using (var db = new Model1())
                {
                    var query = from b in db.Companies
                                orderby b.CompanyName
                                select b;
                    foreach (var item in query)
                    {
                        if (item.CompanyName.ToLower().Contains(CompanySearchBar.Text.ToLower()))
                        {
                            ListBoxItem lbi = new ListBoxItem { Content = item.CompanyName + " | " + item.CompanyName_Formal };
                            lbi.Tag = item.CompanyName;
                            lbi.Selected += Company_Clicked;
                            CompanyListBox.Items.Add(lbi);
                        }
                    }
                }
                if (CompanyListBox.Items.Count == 0)
                {
                    NoResults.Text = "No Results. Try Something Shorter?";
                }
            }
            button.IsEnabled = true;
        }

        //Executes Geocode and LocationRecog for an address
        private void Go_Clicked(object sender, RoutedEventArgs e)
        {
            GoButton.IsEnabled = false;
            geocodeAddress = GeocodeAddressTbx.Text;
            GeocodeBtn_Clicked(sender, e);
            UpdateCompanyButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
        }

        //Sends data from address from Properties table to get a Bing Geocode and LocationRecog request
        private void Address_Clicked(object sender, RoutedEventArgs e)
        {
            ListBoxItem temp = (ListBoxItem)e.OriginalSource;
            AddressInfo chosenInfo = (AddressInfo)temp.Tag;
            GeocodeAddressTbx.Text = GlobalAddressInfo.Address_Full = chosenInfo.Address_Full;
            IDTbx.Text = "" + chosenInfo.PropertyID;
            GlobalAddressInfo.PropertyID = chosenInfo.PropertyID;
            GlobalAddressInfo.Notes = chosenInfo.Notes;
        }

        //Sends URL, Phone, Company Name to be stored, can still be manually changed in the UI
        private void SelectRow_Clicked(object sender, RoutedEventArgs e)
        {
            Button but = (Button) e.OriginalSource;
            AddressInfo displayinfo = (AddressInfo) but.Tag;

            CompanySearchBar.Text = displayinfo.EntityName;
            GlobalAddressInfo.URL = displayinfo.URL;
            GlobalAddressInfo.Phone = displayinfo.Phone;

            UpdateCompanyButton.IsEnabled = false;
        }

        //Handles what happens when an existing company is clicked
        private void Company_Clicked(object sender, RoutedEventArgs e)
        {
            var lbi = (ListBoxItem) sender;
            SelectedCompanyName = lbi.Tag.ToString();
            UpdateCompanyButton.IsEnabled = true;
        }

        //Attempts to store the data into database
        private void SaveButton_Clicked(object sender, RoutedEventArgs e)
        {
            SaveNotification.Foreground = Brushes.Red;
            SaveNotification.Text = "";
            if(NewNameBox.Text=="")
                SaveNotification.Text = "Enter a Name For New Company";
            else
            {
                using (var db = new Model1())
                {
                    //Updating Companies Table
                    var company = new Companies();
                    if (SaveButton.Content.ToString() == "Save Company Info")
                    {
                        company = db.Companies.FirstOrDefault(c => c.CompanyName == NewNameBox.Text);
                        if (!String.IsNullOrEmpty(NewURLBox.Text))
                            company.URL = NewURLBox.Text;
                        if (!String.IsNullOrEmpty(NewPhoneNumBox.Text)) 
                            company.PrimaryPhone = NewPhoneNumBox.Text;
                        company.Updated = DateTime.Now;
                    }
                    else if (SaveButton.Content.ToString() == "Add New Company")
                    {
                        company = new Companies { CompanyName = NewNameBox.Text, CompanyName_Formal = NewNameBox.Text, Created = DateTime.Now, RecordSource = GlobalAddressInfo.RecordSource};
                        if (!String.IsNullOrEmpty(NewURLBox.Text))
                            company.URL = NewURLBox.Text;
                        if (!String.IsNullOrEmpty(NewPhoneNumBox.Text))
                            company.PrimaryPhone = NewPhoneNumBox.Text;
                        db.Companies.Add(company);
                    }
                    db.SaveChanges();
                    SaveNotification.Foreground = Brushes.Green;
                    SaveNotification.Text = "Saved";

                    //Updating Properties Table
                    var property = db.Properties.FirstOrDefault(p => p.ID == GlobalAddressInfo.PropertyID);
                    property.Lattitude = GlobalAddressInfo.Lat;
                    property.Longitude = GlobalAddressInfo.Long;
                    property.Updated = DateTime.Now;
                    property.LocalPhoneNumber = GlobalAddressInfo.Phone;
                    var psc = db.D1PropertyStatusCodes.FirstOrDefault(x => x.Description == PropertyStatusMenu.Header.ToString());
                    if (psc != null)
                        property.PropertyStatus = psc.CodeName;
                    else
                        property.PropertyStatus = "UNKNOWN";
                    db.SaveChanges();

                    //Updating PropertyTenants Table
                    var propten = new PropertyTenants();
                    propten.CompanyID = company.ID;
                    propten.PropertyID = GlobalAddressInfo.PropertyID;
                    var csc = db.D1ClientStatusCodes.FirstOrDefault(x => x.Description == ClientStatusMenu.Header.ToString());
                    if (csc != null)
                        propten.ClientStatus = csc.CodeName;
                    else
                        propten.ClientStatus = "UNKNOWN";
                    var tt = db.D1TargetTypes.FirstOrDefault(x => x.Description == TargetTypeMenu.Header.ToString());
                    if (tt != null)
                        propten.TargetType = tt.TypeName;
                    else
                        propten.TargetType = "UNKNOWN";
                    propten.Created = DateTime.Now;
                    if (!String.IsNullOrEmpty(NotesBox.Text))
                        propten.Notes = NotesBox.Text;

                    db.PropertyTenants.Add(propten);
                    //If a property and a company has already been matched up once, it cannot be done again because the combination of the 2 ID's is used as a key
                    //Couldn't figure out a work-around in the time left
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        SaveNotification.Text = "Duplicate PropertyTenant";
                    }
                }
            }
            SaveButton.IsEnabled = false;
            NewNameBox.Text = "";
            NewPhoneNumBox.Text = "";
            NewURLBox.Text = "";
            UpdateCompanyButton.IsEnabled = false;
        }

        //Handles what happens when "Add New Company" or "Update This Company" is pressed
        private void UpdateCompany(object sender, RoutedEventArgs e)
        {
            NewURLBox.Text = GlobalAddressInfo.URL;
            NewPhoneNumBox.Text = GlobalAddressInfo.Phone;
            Button but = (Button)sender;
            if (but.Content.ToString() == "Update This Company")
            {
                SaveButton.Content = "Save Company Info";
                NewNameBox.IsEnabled = false;
                NewNameBox.Text = SelectedCompanyName;
            }
            else if (but.Content.ToString() == "Add New Company")
            {
                SaveButton.Content = "Add New Company";
                NewNameBox.IsEnabled = true;
                NewNameBox.Text = CompanySearchBar.Text;
            }
            UpdatePanel.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }

        //If URL in the table view is clicked, opens it up
        private void Url_Clicked(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }
        
        //Changes search parameters, but "Go" or Enter must be pressed again
        private void ResultsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            NumResults = (int) e.NewValue;
        }
        private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SearchRadius = (double)e.NewValue;
        }

        #endregion

        #region Unused Methods
        ///// <summary>
        ///// Demostrates how to make a Reverse Geocode Request.
        ///// </summary>
        //private void ReverseGeocodeBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ReverseGeocodeRequest()
        //    {
        //        Point = new Coordinate(45, -110),
        //        IncludeEntityTypes = new List<EntityType>(){
        //            EntityType.AdminDivision1,
        //            EntityType.CountryRegion
        //        },
        //        IncludeNeighborhood = true,
        //        IncludeIso2 = true,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        //private void LocalSearchBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new LocalSearchRequest()
        //    {
        //        Query = "coffee",
        //        MaxResults = 25,
        //        UserLocation = new Coordinate(47.602038, -122.333964),
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        //private void LocalSearchTypeBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new LocalSearchRequest()
        //    {
        //        Types = new List<string>() { "CoffeeAndTea" },
        //        MaxResults = 25,
        //        UserLocation = new Coordinate(47.602038, -122.333964),
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}


        //private void LocalInsightsBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new LocalInsightsRequest()
        //    {
        //        Types = new List<string>() { "CoffeeAndTea" },
        //        Waypoint = new SimpleWaypoint("Bellevue, WA"),
        //        MaxTime = 60,
        //        TimeUnit = TimeUnitType.Minute,
        //        DateTime = DateTime.Now.AddMinutes(15),
        //        TravelMode = TravelModeType.Driving,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        //private void AutosuggestBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new AutosuggestRequest()
        //    {
        //        BingMapsKey = BingMapsKey,
        //        Query = "El Bur",
        //        UserLocation = new CircularView(47.668697, -122.376373, 5),
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make an Elevation Request.
        ///// </summary>
        //private void ElevationBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ElevationRequest()
        //    {
        //        Points = new List<Coordinate>(){
        //            new Coordinate(45, -100),
        //            new Coordinate(50, -100),
        //            new Coordinate(45, -110)
        //        },
        //        Samples = 1024,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}


        ///// <summary>
        ///// Demostrates how to make an Elevation Request for a bounding box.
        ///// </summary>
        //private void ElevationByBboxBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ElevationRequest()
        //    {
        //        Bounds = new BoundingBox(new double[] {50.995391, -1.320763, 52.000577, -2.311836}),
        //        Row = 50,
        //        Col = 4,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Driving Route Request.
        ///// </summary>
        //private void RouteBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new RouteRequest()
        //    {
        //        RouteOptions = new RouteOptions(){
        //            Avoid = new List<AvoidType>()
        //            {
        //                AvoidType.MinimizeTolls
        //            },
        //            TravelMode = TravelModeType.Driving,
        //            DistanceUnits = DistanceUnitType.Miles,
        //            Heading = 45,
        //            RouteAttributes = new List<RouteAttributeType>()
        //            {
        //                RouteAttributeType.RoutePath
        //            },
        //            Optimize = RouteOptimizationType.TimeWithTraffic
        //        },
        //        Waypoints = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(){
        //                Address = "Seattle, WA"
        //            },
        //            new SimpleWaypoint(){
        //                Address = "Bellevue, WA",
        //                IsViaPoint = true
        //            },
        //            new SimpleWaypoint(){
        //                Address = "Redmond, WA"
        //            }
        //        },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Driving Route Request that has more than 25 waypoints.
        ///// </summary>
        //private void LongRouteBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new RouteRequest()
        //    {
        //        RouteOptions = new RouteOptions()
        //        {
        //            Avoid = new List<AvoidType>()
        //            {
        //                AvoidType.MinimizeTolls
        //            },
        //            TravelMode = TravelModeType.Driving,
        //            DistanceUnits = DistanceUnitType.Miles,
        //            Heading = 45,
        //            RouteAttributes = new List<RouteAttributeType>()
        //            {
        //                RouteAttributeType.RoutePath
        //            },
        //            Optimize = RouteOptimizationType.TimeWithTraffic
        //        },
        //        Waypoints = new List<SimpleWaypoint>() //29 waypoints, more than what the routing service normally handles, so the request will break this up into two requests and merge the results.
        //        {
        //            new SimpleWaypoint(47.5886, -122.336),
        //            new SimpleWaypoint(47.5553, -122.334),
        //            new SimpleWaypoint(47.5557, -122.316),
        //            new SimpleWaypoint(47.5428, -122.322),
        //            new SimpleWaypoint(47.5425, -122.341),
        //            new SimpleWaypoint(47.5538, -122.362),
        //            new SimpleWaypoint(47.5647, -122.384),
        //            new SimpleWaypoint(47.5309, -122.380),
        //            new SimpleWaypoint(47.5261, -122.351),
        //            new SimpleWaypoint(47.5137, -122.382),
        //            new SimpleWaypoint(47.5101, -122.337),
        //            new SimpleWaypoint(47.4901, -122.341),
        //            new SimpleWaypoint(47.4850, -122.320),
        //            new SimpleWaypoint(47.5024, -122.263),
        //            new SimpleWaypoint(47.4970, -122.226),
        //            new SimpleWaypoint(47.4736, -122.265),
        //            new SimpleWaypoint(47.4562, -122.287),
        //            new SimpleWaypoint(47.4452, -122.338),
        //            new SimpleWaypoint(47.4237, -122.292),
        //            new SimpleWaypoint(47.4230, -122.257),
        //            new SimpleWaypoint(47.3974, -122.249),
        //            new SimpleWaypoint(47.3765, -122.277),
        //            new SimpleWaypoint(47.3459, -122.302),
        //            new SimpleWaypoint(47.3073, -122.280),
        //            new SimpleWaypoint(47.3115, -122.228),
        //            new SimpleWaypoint(47.2862, -122.218),
        //            new SimpleWaypoint(47.2714, -122.294),
        //            new SimpleWaypoint(47.2353, -122.306),
        //            new SimpleWaypoint(47.1912, -122.408)
        //        },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);

        //    RequestUrlTbx.Text = "Request broken up into multiple sub-requests.";
        //}

        ///// <summary>
        ///// Demostrates how to make a Transit Route Request.
        ///// </summary>
        //private void TransitRouteBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new RouteRequest()
        //    {
        //        RouteOptions = new RouteOptions()
        //        {
        //            TravelMode = TravelModeType.Transit,
        //            DistanceUnits = DistanceUnitType.Miles,
        //            RouteAttributes = new List<RouteAttributeType>()
        //            {
        //                RouteAttributeType.RoutePath,
        //                RouteAttributeType.TransitStops
        //            },
        //            Optimize = RouteOptimizationType.TimeAvoidClosure,
        //            DateTime = DateTime.Now,
        //            TimeType = RouteTimeType.Departure
        //        },
        //        Waypoints = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(){
        //                Address = "London, UK"
        //            },
        //            new SimpleWaypoint(){
        //                Address = "E14 3SP"
        //            }
        //        },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Truck Routing Request.
        ///// </summary>
        //private void TruckRouteBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new RouteRequest()
        //    {
        //        RouteOptions = new RouteOptions()
        //        {
        //            Avoid = new List<AvoidType>()
        //            {
        //                AvoidType.MinimizeTolls
        //            },
        //            TravelMode = TravelModeType.Truck,
        //            DistanceUnits = DistanceUnitType.Miles,
        //            Heading = 45,
        //            RouteAttributes = new List<RouteAttributeType>()
        //            {
        //                RouteAttributeType.RoutePath
        //            },
        //            Optimize = RouteOptimizationType.Time,
        //            VehicleSpec = new VehicleSpec()
        //            {
        //                VehicleWidth = 3,
        //                VehicleHeight = 5,
        //                DimensionUnit = DimensionUnitType.Meter,
        //                VehicleWeight = 15000,
        //                WeightUnit = WeightUnitType.Pound,
        //                VehicleHazardousMaterials = new List<HazardousMaterialType>()
        //                {
        //                    HazardousMaterialType.Combustable,
        //                    HazardousMaterialType.Flammable
        //                }
        //            }
        //        },
        //        Waypoints = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(){
        //                Address = "Seattle, WA"
        //            },
        //             new SimpleWaypoint(){
        //                Address = "Bellevue, WA",
        //                IsViaPoint = true
        //            },
        //            new SimpleWaypoint(){
        //                Address = "Redmond, WA"
        //            }
        //        },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Multi-route optimization Request.
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void OptimizeItineraryBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new OptimizeItineraryRequest()
        //    {
        //        Agents = new List<Agent>()
        //        {
        //            new Agent()
        //            {
        //                Name = "agent1",
        //                Shifts = new List<Shift>()
        //                {
        //                    new Shift()
        //                    {
        //                        StartTimeUtc = new DateTime(2022, 1, 1, 8, 0, 0), //8 am
        //                        StartLocation = new SimpleWaypoint("1603 NW 89th St, Seattle, WA 98117, US"),
        //                        EndTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0), //6pm
        //                        EndLocation = new SimpleWaypoint(47.7070790545669, -122.355226696231),
        //                        Breaks = new Break[]
        //                        {
        //                            new Break()
        //                            {
        //                                StartTimeUtc = new DateTime(2022, 1, 1, 12, 0, 0), //12pm/noon
        //                                EndTimeUtc = new DateTime(2022, 1, 1, 14, 0, 0),   //2pm
        //                                DurationTimeSpan = new TimeSpan(0, 30, 0) //30 minutes.
        //                            },
        //                            new Break()
        //                            {
        //                                StartTimeUtc = new DateTime(2022, 1, 1, 16, 0, 0), //4pm
        //                                EndTimeUtc = new DateTime(2022, 1, 1, 16, 30, 0)   //4:30pm
        //                            }
        //                        }
        //                    }
        //                },
        //                Price = new Price()
        //                {
        //                    FixedPrice = 100,
        //                    PricePerHour = 5,
        //                    PricePerKM = 1
        //                },
        //                Capacity = new int[] { 16 }
        //            }
        //        },
        //        ItineraryItems = new List<OptimizeItineraryItem>()
        //        {
        //            new OptimizeItineraryItem()
        //            {
        //                Name = "Customer 1",
        //                OpeningTimeUtc = new DateTime(2022, 1, 1, 9, 0, 0), //9am
        //                ClosingTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0),   //6pm
        //                DwellTimeSpan = new TimeSpan(0, 32, 0), //32 minutes
        //                Priority = 5,
        //                Quantity = new int[] { 4 },
        //                //Waypoint = new SimpleWaypoint(47.692290770423,-122.385954752402),
        //                Waypoint = new SimpleWaypoint("8712 Jones Pl NW, Seattle, WA 98117, US")
        //            },
        //            new OptimizeItineraryItem()
        //            {
        //                Name = "Customer 2",
        //                OpeningTimeUtc = new DateTime(2022, 1, 1, 9, 0, 0), //9am
        //                ClosingTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0),   //6pm
        //                DwellTimeSpan = new TimeSpan(1, 34, 0), //1 hour 34 minutes
        //                Priority = 16,
        //                Quantity = new int[] { -3 },
        //                Waypoint = new SimpleWaypoint(47.6962193175262,-122.342180147243),
        //                DropOffFrom = new string[] { "Customer 3" }
        //            },
        //            new OptimizeItineraryItem()
        //            {
        //                Name = "Customer 3",
        //                OpeningTimeUtc = new DateTime(2022, 1, 1, 9, 0, 0), //9am
        //                ClosingTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0),   //6pm
        //                DwellTimeSpan = new TimeSpan(1, 0, 0), //1 hour
        //                Priority = 88,
        //                Quantity = new int[] { 3 },
        //                Waypoint = new SimpleWaypoint(47.6798098928389,-122.383036445391)
        //            },
        //            new OptimizeItineraryItem()
        //            {
        //                Name = "Customer 4",
        //                OpeningTimeUtc = new DateTime(2022, 1, 1, 9, 0, 0), //9am
        //                ClosingTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0),   //6pm
        //                DwellTimeSpan = new TimeSpan(0, 25, 0), //25 minutes
        //                Priority = 3,
        //                Quantity = new int[] { -3 },
        //                Waypoint = new SimpleWaypoint(47.6867440824094,-122.354711700877),
        //                DropOffFrom = new string[] { "Customer 1" }
        //            },
        //            new OptimizeItineraryItem()
        //            {
        //                Name = "Customer 5",
        //                OpeningTimeUtc = new DateTime(2022, 1, 1, 9, 0, 0), //9am
        //                ClosingTimeUtc = new DateTime(2022, 1, 1, 18, 0, 0),   //6pm
        //                DwellTimeSpan = new TimeSpan(0, 18, 0), //18 minutes
        //                Priority = 1,
        //                Quantity = new int[] { -1 },
        //                Waypoint = new SimpleWaypoint(47.6846639223203,-122.364839942855),
        //                DropOffFrom = new string[] { "Customer 1" }
        //            }
        //         },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Traffic Request.
        ///// </summary>
        //private void TrafficBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new TrafficRequest()
        //    {
        //        Culture = "en-US",
        //        TrafficType = new List<TrafficType>()
        //        {
        //            TrafficType.Accident,
        //            TrafficType.Congestion
        //        },
        //        //Severity = new List<SeverityType>()
        //        //{
        //        //    SeverityType.LowImpact,
        //        //    SeverityType.Minor
        //        //},
        //        MapArea = new BoundingBox()
        //        {
        //            SouthLatitude = 46,
        //            WestLongitude = -124,
        //            NorthLatitude = 50,
        //            EastLongitude = -117
        //        },
        //        IncludeLocationCodes = true,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make an Imagery Metadata Request.
        ///// </summary>
        //private void ImageMetadataBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ImageryMetadataRequest()
        //    {
        //        CenterPoint = new Coordinate(45, -110),
        //        ZoomLevel = 12,
        //        ImagerySet = ImageryType.AerialWithLabels,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Static Imagery Metadata Request.
        ///// </summary>
        //private void StaticImageMetadataBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ImageryRequest()
        //    {
        //        CenterPoint = new Coordinate(45, -110),
        //        ZoomLevel = 12,
        //        ImagerySet = ImageryType.AerialWithLabels,
        //        Pushpins = new List<ImageryPushpin>(){
        //            new ImageryPushpin(){
        //                Location = new Coordinate(45, -110.01),
        //                Label = "hi"
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(45, -110.02),
        //                IconStyle = 3
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(45, -110.03),
        //                IconStyle = 20
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(45, -110.04),
        //                IconStyle = 24
        //            }
        //        },
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Static Imagery Request.
        ///// </summary>
        //private void StaticImageBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new ImageryRequest()
        //    {
        //        CenterPoint = new Coordinate(45, -110),
        //        ZoomLevel = 1,
        //        ImagerySet = ImageryType.RoadOnDemand,
        //        Pushpins = new List<ImageryPushpin>(){
        //            new ImageryPushpin(){
        //                Location = new Coordinate(45, -110.01),
        //                Label = "hi"
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(30, -100),
        //                IconStyle = 3
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(25, -80),
        //                IconStyle = 20
        //            },
        //            new ImageryPushpin(){
        //                Location = new Coordinate(33, -75),
        //                IconStyle = 24
        //            }
        //        },
        //        BingMapsKey = BingMapsKey,
        //        Style = @"{
        //         ""version"": ""1.*"",
        //         ""settings"": {
        //          ""landColor"": ""#0B334D""
        //         },
        //         ""elements"": {
        //          ""mapElement"": {
        //           ""labelColor"": ""#FFFFFF"",
        //           ""labelOutlineColor"": ""#000000""
        //          },
        //          ""political"": {
        //           ""borderStrokeColor"": ""#144B53"",
        //           ""borderOutlineColor"": ""#00000000""
        //          },
        //          ""point"": {
        //           ""iconColor"": ""#0C4152"",
        //           ""fillColor"": ""#000000"",
        //           ""strokeColor"": ""#0C4152""
        //                },
        //          ""transportation"": {
        //           ""strokeColor"": ""#000000"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""highway"": {
        //           ""strokeColor"": ""#158399"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""controlledAccessHighway"": {
        //           ""strokeColor"": ""#158399"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""arterialRoad"": {
        //           ""strokeColor"": ""#157399"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""majorRoad"": {
        //           ""strokeColor"": ""#157399"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""railway"": {
        //           ""strokeColor"": ""#146474"",
        //           ""fillColor"": ""#000000""
        //          },
        //          ""structure"": {
        //           ""fillColor"": ""#115166""
        //          },
        //          ""water"": {
        //           ""fillColor"": ""#021019""
        //          },
        //          ""area"": {
        //           ""fillColor"": ""#115166""
        //          }
        //         }
        //        }"
        //    };

        //    ProcessImageRequest(r);           
        //}

        ///// <summary>
        ///// Demostrates how to make a Geospatial Endpoint Request.
        ///// </summary>
        //private void GeospatialEndpointBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new GeospatialEndpointRequest()
        //    {
        //        //Language = "zh-CN",
        //        Culture = "zh-CN",
        //        UserRegion = "CN",
        //        UserLocation = new Coordinate(40, 116),
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Distance Matrix Request.
        ///// </summary>
        //private void DistanceMatrixBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new DistanceMatrixRequest()
        //    {
        //        Origins = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(47.6044, -122.3345),
        //            new SimpleWaypoint(47.6731, -122.1185),
        //            new SimpleWaypoint(47.6149, -122.1936)
        //        },
        //        Destinations = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(45.5347, -122.6231),
        //            new SimpleWaypoint(47.4747, -122.2057),
        //        },
        //        BingMapsKey = BingMapsKey,
        //        TimeUnits = TimeUnitType.Minute,
        //        DistanceUnits = DistanceUnitType.Miles,
        //        TravelMode = TravelModeType.Driving
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make a Distance Matrix Histogram Request.
        ///// </summary>
        //private void DistanceMatrixHistogramBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new DistanceMatrixRequest()
        //    {
        //        Origins = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(47.6044, -122.3345),
        //            new SimpleWaypoint(47.6731, -122.1185),
        //            new SimpleWaypoint(47.6149, -122.1936)
        //        },
        //        Destinations = new List<SimpleWaypoint>()
        //        {
        //            new SimpleWaypoint(45.5347, -122.6231),
        //            new SimpleWaypoint(47.4747, -122.2057)
        //        },
        //        BingMapsKey = BingMapsKey,
        //        StartTime = DateTime.Now,
        //        EndTime = DateTime.Now.AddHours(8),
        //        Resolution = 4
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make an Isochrone Request.
        ///// </summary>
        //private void IsochroneBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new IsochroneRequest()
        //    {
        //        Waypoint = new SimpleWaypoint("Bellevue, WA"),
        //        MaxTime = 60,
        //        TimeUnit = TimeUnitType.Minute,
        //        DateTime = DateTime.Now.AddMinutes(15),
        //        TravelMode = TravelModeType.Driving,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        ///// <summary>
        ///// Demostrates how to make an Snap to Road Request.
        ///// </summary>
        //private void SnapToRoadBtn_Clicked(object sender, RoutedEventArgs e)
        //{
        //    var r = new SnapToRoadRequest()
        //    {
        //        Points = new List<Coordinate>()
        //        {
        //            new Coordinate(47.590868, -122.336729),
        //            new Coordinate(47.594994, -122.334263),
        //            new Coordinate(47.601604, -122.336042),
        //            new Coordinate(47.60849, -122.34241),
        //            new Coordinate(47.610568, -122.345064)
        //        },
        //        IncludeSpeedLimit = true,
        //        IncludeTruckSpeedLimit = true,
        //        Interpolate = true,
        //        SpeedUnit = SpeedUnitType.MPH,
        //        TravelMode = TravelModeType.Driving,
        //        BingMapsKey = BingMapsKey
        //    };

        //    ProcessRequest(r, sender, e);
        //}

        #endregion

        #region Private Methods

        //Executes Bing Request for Geocode AND LocationRecog
        private async void ProcessRequest(BaseRestRequest request, object sender, RoutedEventArgs e)
        {
            try
            {
                RequestProgressBar.Visibility = Visibility.Visible;
                RequestProgressBarText.Text = string.Empty;

                ResultTreeView.ItemsSource = null;

                var start = DateTime.Now;

                //Execute the request.
                var response = await request.Execute((remainingTime) =>
                {
                    if (remainingTime > -1)
                    {
                        _time = TimeSpan.FromSeconds(remainingTime);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            RequestProgressBarText.Text = string.Format("Time remaining {0} ", _time);
                        });

                        _timer.Start();
                    }
                });
                
                RequestUrlTbx.Text = request.GetRequestUrl();

                var end = DateTime.Now;

                var processingTime = end - start;

                ProcessingTimeTbx.Text = string.Format(CultureInfo.InvariantCulture, "{0:0} ms", processingTime.TotalMilliseconds);
                RequestProgressBar.Visibility = Visibility.Collapsed;

                var nodes = new List<ObjectNode>();
                var tree = await ObjectNode.ParseAsync("result", response);
                nodes.Add(tree);
                ResultTreeView.ItemsSource = nodes;
                
                //after geocode request
                if(mode==1)
                {
                    coord1 = Convert.ToDouble(tree.Children[6].Children[0].Children[1].Children[0].Children[1].Children[1].Children[0].Value.ToString());
                    coord2 = Convert.ToDouble(tree.Children[6].Children[0].Children[1].Children[0].Children[1].Children[1].Children[1].Value.ToString());
                    GeocodeCompleted.Invoke(sender, e);
                }

                //after location recog request
                if(mode==2)
                {
                    //Adds first row
                    TableRow r = new TableRow { FontSize = 14 };
                    RowGroup.Rows.Clear();
                    r.Cells.Add(new TableCell(new Paragraph(new Run("Formatted Add"))));
                    r.Cells.Add(new TableCell(new Paragraph(new Run("Name"))));
                    r.Cells.Add(new TableCell(new Paragraph(new Run("URL"))));
                    r.Cells.Add(new TableCell(new Paragraph(new Run("Phone Number"))));
                    r.Cells.Add(new TableCell(new Paragraph(new Run("Type"))));
                    RowGroup.Rows.Add(r);

                    RowGroup.Rows.Capacity = 100;

                    var ListOfPins = tree.Children[6].Children[0].Children[1].Children[0].Children[2];
                    int coloralternator = 0;

                    for (int i=0;i<ListOfPins.Children.Count;i++)
                    {
                        TableRow row = new TableRow();

                        string address = "";
                        string name = "";
                        string url = "";
                        string phone = "";
                        string type = "";

                        //Pulls address, coordinates of locations
                        for (int j = 0; j < ListOfPins.Children[i].Children[0].Children.Count; j++)
                        {
                            if (ListOfPins.Children[i].Children[0].Children[j].Name.ToString() == "FormattedAddress")
                            {
                                address = ListOfPins.Children[i].Children[0].Children[j].Value.ToString().Replace("\"", "");
                                row.Cells.Add(new TableCell(new Paragraph(new Run(address))));
                            }
                            if (ListOfPins.Children[i].Children[0].Children[j].Name.ToString() == "Latitude")
                            {
                                PushpinsData[i, 0] = Convert.ToDouble(ListOfPins.Children[i].Children[0].Children[j].Value.ToString().Replace("\"", ""));
                            }
                            if (ListOfPins.Children[i].Children[0].Children[j].Name.ToString() == "Longitude")
                            {
                                PushpinsData[i, 1] = Convert.ToDouble(ListOfPins.Children[i].Children[0].Children[j].Value.ToString().Replace("\"", ""));
                            }
                        }

                        bool NameFlag = false;
                        bool URLFlag = false;
                        bool PhoneFlag = false;
                        bool TypeFlag = false;

                        //Pulls data of locations to format into table, and the map
                        for (int j=0;j<ListOfPins.Children[i].Children[1].Children.Count;j++)
                        {
                            if (ListOfPins.Children[i].Children[1].Children[j].Name.ToString()=="EntityName")
                            {
                                name = ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "");
                                row.Cells.Add(new TableCell(new Paragraph(new Run(name))));
                                PushpinsData[i, 2] = ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "");
                                NameFlag = true;
                            }
                            if (ListOfPins.Children[i].Children[1].Children[j].Name.ToString() == "URL")
                            {
                                Hyperlink hl = new Hyperlink();
                                Run run = new Run { Text = ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "") };
                                if(!ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "").StartsWith("htt"))
                                    hl.NavigateUri = new Uri("http://" + ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", ""));
                                else
                                    hl.NavigateUri = new Uri(ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", ""));
                                url = hl.NavigateUri.ToString();
                                hl.Inlines.Add(run);
                                hl.RequestNavigate += Url_Clicked;
                                row.Cells.Add(new TableCell(new Paragraph(hl)));
                                URLFlag = true;
                            }
                            if (ListOfPins.Children[i].Children[1].Children[j].Name.ToString() == "Phone")
                            {
                                phone = ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "");
                                row.Cells.Add(new TableCell(new Paragraph(new Run(phone))));
                                PhoneFlag = true;
                            }
                            if (ListOfPins.Children[i].Children[1].Children[j].Name.ToString() == "Type")
                            {
                                type = ListOfPins.Children[i].Children[1].Children[j].Value.ToString().Replace("\"", "");
                                row.Cells.Add(new TableCell(new Paragraph(new Run(type))));
                                TypeFlag = true;
                            }
                        }

                        //Formatting for empty information
                        if (!NameFlag)
                            row.Cells.Insert(1, new TableCell(new Paragraph(new Run(""))));
                        if (!URLFlag)
                            row.Cells.Insert(2, new TableCell(new Paragraph(new Run(""))));
                        if (!PhoneFlag)
                            row.Cells.Insert(3, new TableCell(new Paragraph(new Run(""))));
                        if (!TypeFlag)
                            row.Cells.Insert(4, new TableCell(new Paragraph(new Run(""))));

                        //Add Store Button to end of row, push row into table
                        Button selectRow = new Button { Content = "Select Row" };
                        selectRow.Tag = new AddressInfo(address, name, url, phone, type);
                        selectRow.Click += SelectRow_Clicked;
                        row.Cells.Add(new TableCell(new BlockUIContainer(selectRow)));
                        if (coloralternator % 2 == 0)
                            row.Background = Brushes.LightGoldenrodYellow;
                        else
                            row.Background = Brushes.FloralWhite;
                        coloralternator++;
                        RowGroup.Rows.Add(row);
                    }

                    //Data From AddressAtLocation
                    var AddressData = tree.Children[6].Children[0].Children[1].Children[0].Children[3].Children[0];
                    for (int i = 0; i < AddressData.Children.Count; i++)
                    {
                        if (AddressData.Children[i].Name == "Latitude")
                            GlobalAddressInfo.Lat = AddressData.Children[i].Value.ToString().Replace("\"", "");
                        if (AddressData.Children[i].Name == "Longitude")
                            GlobalAddressInfo.Long = AddressData.Children[i].Value.ToString().Replace("\"", "");
                        if (AddressData.Children[i].Name == "FormattedAddress")
                            GlobalAddressInfo.AddressAtLocation = AddressData.Children[i].Value.ToString().Replace("\"", "");
                    }
                    GoButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            _timer.Stop();
            RequestProgressBar.Visibility = Visibility.Collapsed;
        }


        #endregion

    }
}
