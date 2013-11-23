//#define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;


namespace Media_Inventory_Manager
{

    //------------------------------------    pricing service    --------------------------------------------
    public class RePricingRoutines
    {
        public RePricingRoutines() {
            mf = new mainForm(false);
        }

        public int totalRecordCount = 0;
        public int recordCounter = 0;
        internal mainForm mf;

        internal struct sUPCList  //  contains a list of UPCs to send to services
        {
            internal string UPC;
            internal string SKU;
            internal char mediaType;  //  New or Used
            internal decimal Price;
        };

        internal struct highLowAverage
        {
            internal decimal highPrice;  // high
            internal decimal lowPrice;  //  low
            internal decimal averagePrice;  //  average
            internal decimal listPrice;  //  list price
            internal decimal accumulatedPrice;
        };


        //--------------------    pricing service (called from mainForm)   ----------------
        public void doPricingService(ArrayList parms) {
            Cursor.Current = Cursors.AppStarting;

            //  set up parms
            FbConnection mediaConn = (FbConnection)parms[0];
            FbDataReader dr = (FbDataReader)parms[1];
            Label pss = (Label)parms[2];
            Label lTimeRemaining = (Label)parms[9];
            Button bMarkAll = (Button)parms[12];
            Button bPricingServiceUpdate = (Button)parms[13];
            Button bStartPricingService = (Button)parms[14];
            Button bStopPricingService = (Button)parms[15];
            CheckBox cbAmazonPrice = (CheckBox)parms[17];
            TextBox tbAssocTag = (TextBox)parms[47];  //  new 11.5.0

            CheckBox cbCombinedNewUsed = (CheckBox)parms[18];
            //ComboBox coCondition = (ComboBox)parms[8];
            Label lProgress = (Label)parms[54];
            TextBox AWSKey = (TextBox)parms[55];
            TextBox AWSSecretKey = (TextBox)parms[57];
            TabControl tabTaskPanel = (TabControl)parms[58];
            int cWebPages = (int)parms[59];
            char mediaCondn = ' ';

            List<sUPCList> UPCList = new List<sUPCList>();
            getMediaCountfromDB(UPCList, parms);  //  get media count from d/b

            int counter = UPCList.Count;

            //  get starting time
            double actualStartTime;
            double itemStartTime;
            double currentTime = 0; 
            double elapsedSeconds = 0;
            double avgSecondsPerItem = 0;
            actualStartTime = DateTime.Now.TimeOfDay.TotalSeconds;

            //  tell 'em what we're doing...
            pss.Text = "Submitting " + UPCList.Count + " items to the pricing routines...";
            pss.Refresh();
            string sku = "";

            //AmazonWebServices aws = new AmazonWebServices();
            //BestWebBuysDotCom bwp = new BestWebBuysDotCom();
            //mainForm.bookData bD = new mainForm.bookData();

            for (int i = 0; i < counter; i++) //  process each UPC in UPCList
            {
                AmazonWebServices aws = new AmazonWebServices();
                //BestWebBuysDotCom bwp = new BestWebBuysDotCom();

                mainForm.mediaData bD = new mainForm.mediaData();

                ////  look to see if we are supposed to process this book
                //if (cbExcludeAbove.Checked == true && tbExcludeAboveAmt.Text.Length > 0)
                //{
                //    if (UPCList[i].Price > decimal.Parse(tbExcludeAboveAmt.Text))
                //        continue;
                //}

                //if (cbExcludeBelow.Checked == true && tbExcludeBelowAmt.Text.Length > 0)
                //{
                //    if (UPCList[i].Price < decimal.Parse(tbExcludeBelowAmt.Text))
                //        continue;
                //}


                itemStartTime = DateTime.Now.TimeOfDay.TotalSeconds;
                bool rc = true;

                //  list mostly debugging info
                lProgress.Text = "Processing " + (i + 1).ToString() + " of " + counter.ToString() + "    (" + UPCList[i].SKU + ")";


                if (cbAmazonPrice.Checked == true || UPCList[i].UPC.StartsWith("B")) {
                    if (cbCombinedNewUsed.Checked == true)
                        mediaCondn = 'b';
                    else
                        mediaCondn = UPCList[i].mediaType;

                    rc = aws.getPricesFromAmazon(bD, UPCList[i].UPC, mediaCondn, AWSKey.Text, AWSSecretKey.Text, 
                        (TabControl)parms[58], (int)parms[59], (string)parms[47]);

                    //if (rc == false)
                    //    tabTaskPanel.SelectTab(cWebPages);
                }
                //else {
                //    bwp.getBookPrices(UPCList[i].UPC, bD);  //  get all the prices from the internet and place them in pricingData (pD)
                //}


                //  compute high, low and average 
                highLowAverage hla = new highLowAverage();
                hla = computeHighLowAverage(bD, parms, hla, UPCList[i].mediaType);  //  send pricingData to get high, low and average

                //  now compute suggested prices
                decimal sPrice = computeSuggestedPrice(UPCList[i].SKU, parms, hla);

                sku = UPCList[i].SKU;
                displayInListview(parms, sku, hla, sPrice);  //  display in listview

                //  compute amount of time remaining...
                currentTime = DateTime.Now.TimeOfDay.TotalSeconds;  // get current time in whole and fractional hours
                elapsedSeconds = currentTime - actualStartTime;  //   elapsed seconds
                avgSecondsPerItem = (elapsedSeconds / (i + 1));  //  seconds per book
                double secondsRemaining = avgSecondsPerItem * (counter - i);  //  number of seconds remaining = seconds per book * number of books remaining
                TimeSpan ts = TimeSpan.FromSeconds(secondsRemaining);
                string time = ts.Hours.ToString() + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");

                lTimeRemaining.Text = "Time remaining: " + time + " ( " + Math.Round(avgSecondsPerItem, 1) + " seconds per item)";
                //lProgress.Text = "Processing " + (i + 1).ToString() + " of " + counter.ToString();

                Application.DoEvents();
            }

            Cursor.Current = Cursors.Default;

            pss.Text = "Re-pricing completed...";

            bMarkAll.Enabled = true;
            bPricingServiceUpdate.Enabled = true;
            bStartPricingService.Enabled = true;
            bStopPricingService.Enabled = false;

            Cursor.Current = Cursors.Default;

        }


        //------------------------------    compute high, low and average    -------------------------------------------------
        private highLowAverage computeHighLowAverage(mainForm.mediaData pD, ArrayList parms, highLowAverage hla, char mediaCondition) {
            hla.highPrice = 0.0M;  // high
            hla.lowPrice = 9999.99M;  //  low
            hla.averagePrice = 0.00M;  //  average
            hla.listPrice = 0.0M;  //  list price
            hla.accumulatedPrice = 0.00M;

            string discardAboveAmt = parms[10].ToString().Length == 0 ? "9999.99" : (string)parms[10];
            string discardBelowAmt = parms[11].ToString().Length == 0 ? "0.00" : (string)parms[11];
            CheckBox cbAmazon = (CheckBox)parms[17];
            CheckBox cbCombineNewUsed = (CheckBox)parms[18];

            int k = 0;
            foreach (string key in pD.mediaList.Keys) {
                if (!mainForm.IsNumeric(pD.mediaList[key].price))
                    continue;  //  price is missing, so ignore it...
                //if (pD.bookList[key].price == null)  //  validate
                //pD.bookList[key].price = "0.00";

                if (decimal.Parse(pD.mediaList[key].price) == 0.00M || decimal.Parse(pD.mediaList[key].price) == 999989.00M)  //  we're done
                    break;

                //  if we are not combining new and used media prices, drop if item condition doesn't match the price...
                if (cbCombineNewUsed.Checked == false) {
                    //  if item is new and price is marked as new...
                    if ((mediaCondition == 'n' && pD.mediaList[key].itemCondn != 'n') ||
                        (mediaCondition == 'u' && pD.mediaList[key].itemCondn != 'u'))
                        continue;
                }

                if (decimal.Parse(pD.mediaList[key].price) < decimal.Parse(discardAboveAmt) && decimal.Parse(pD.mediaList[key].price) > decimal.Parse(discardBelowAmt)) {
                    hla.highPrice = hla.highPrice > decimal.Parse(pD.mediaList[key].price) ? hla.highPrice : decimal.Parse(pD.mediaList[key].price);
                    hla.lowPrice = hla.lowPrice < decimal.Parse(pD.mediaList[key].price) ? hla.lowPrice : decimal.Parse(pD.mediaList[key].price);

                    hla.accumulatedPrice += decimal.Parse(pD.mediaList[key].price);  //  accumulate prices
                    k++;  //  count of prices
                }
            }

            decimal tempAvg = 0.00M;
            if (k > 0) {
                tempAvg = hla.accumulatedPrice / k;  //  compute average
                hla.averagePrice = Math.Round(tempAvg, 2);
            }
            else
                hla.averagePrice = 0.00M;

            return hla;

        }



        /*
priceData[0] =  highest price
priceData[1] =  lowest price
priceData[2] =  average
priceData[3] =  list price
priceData[4] =  
* */
        //------------------------------------------------------------------    compute "Suggested Price" here    -----------------------------------------
        private decimal computeSuggestedPrice(string SKU, ArrayList parms, highLowAverage hla) {

            Control.CheckForIllegalCrossThreadCalls = false;

            //  set up all of the parms that were passed
            FbConnection mediaConn = (FbConnection)parms[0];
            CheckBox cbSkipFEandS = (CheckBox)parms[19];
            CheckBox cbExcludeAbove = (CheckBox)parms[20];
            CheckBox cbExcludeBelow = (CheckBox)parms[21];
            TextBox tbExcludeAboveAmt = (TextBox)parms[22];
            TextBox tbExcludeBelowAmt = (TextBox)parms[23];
            RadioButton rbPriceNewFixed = (RadioButton)parms[24];
            RadioButton rbPriceNewPct = (RadioButton)parms[25];
            ListBox lbNewWhatPrice = (ListBox)parms[26];
            TextBox tbNewByAmt = (TextBox)parms[27];
            //      RadioButton rbRepriceWithUPCs = (RadioButton)parms[28];
            CheckBox cbDontGoBelowCost = (CheckBox)parms[29];
            RadioButton rbPriceHighFixed = (RadioButton)parms[30];
            RadioButton rbHighBelow = (RadioButton)parms[31];
            ListBox lbWhatPriceH = (ListBox)parms[32];
            TextBox tbHighByAmt = (TextBox)parms[33];
            RadioButton rbPriceHighPct = (RadioButton)parms[34];
            RadioButton rbLowBelow = (RadioButton)parms[35];
            TextBox tbLowByAmt = (TextBox)parms[36];
            RadioButton rbPriceLowFixed = (RadioButton)parms[37];
            ListBox lbWhatPriceL = (ListBox)parms[38];
            RadioButton rbPriceLowPct = (RadioButton)parms[39];
            TextBox tb1stPremium = (TextBox)parms[40];
            TextBox tbSignedPremium = (TextBox)parms[41];
            TextBox tbCondAmtVG = (TextBox)parms[42];
            TextBox tbCondAmtG = (TextBox)parms[43];
            TextBox tbCondAmtP = (TextBox)parms[44];
            CheckBox cbAdjustPostage = (CheckBox)parms[45];
            TextBox tbBelowMyCostOr = (TextBox)parms[46];

            FbCommand cmd = new FbCommand("SELECT * from tMedia WHERE SKU = '" + SKU + "'", mediaConn);
            FbDataReader dr = null;
            dr = cmd.ExecuteReader();

            while (dr.Read()) {
                //ListViewItem lvi = new ListViewItem(" " + dr["SKU"].ToString());
                //lvi.UseItemStyleForSubItems = false;  //  this allows me to color items

                //lvi.SubItems.Add(dr["UPC"].ToString());
                //int titleLen = dr["Title"].ToString().Length > 45 ? 45 : dr["Title"].ToString().Length;
                //lvi.SubItems.Add(dr["Title"].ToString().Substring(0, titleLen));

                //  re-work price
                string myPrice = dr["Price"].ToString();
                //decimal dPrice = decimal.Parse(myPrice);
                ////dPrice = Math.Round(dPrice, 2);
                //lvi.SubItems.Add(dPrice.ToString("C").Replace("$", ""));


                //if (cbSkipFEandS.Checked == true)  //  don't want to price First Editions or Signed books
                //{
                //    if (dr["Ed"].ToString().Contains("First") || dr["Ed"].ToString().Contains("1st"))
                //        continue;
                //    if (dr["Signed"].ToString().Contains("A") || dr["Signed"].ToString().Contains("I"))
                //        continue;
                //}


                bool ignoreLowPrice;
                bool ignoreHighPrice;
                ignoreLowPrice = cbExcludeBelow.Checked ? true : false;
                ignoreHighPrice = cbExcludeAbove.Checked ? true : false;

                if (dr["UPC"].ToString().Length == 0)
                    continue;

                decimal suggestedPrice = 0.00M;  //  clear it...

                //  is this a new item?
                if (dr["Condn"].ToString().Length != 0 && dr["Condn"].ToString().ToLower().Contains("new")) {
                    if ((rbPriceNewFixed.Checked == true || rbPriceNewPct.Checked == true) && lbNewWhatPrice.SelectedIndex > -1)  //  re-pricing is desired...
                    {
                        if (rbPriceNewFixed.Checked == true)  //  use a fixed amount
                            switch (lbNewWhatPrice.SelectedIndex) {
                                case 0:  //  over cost
                                    suggestedPrice = decimal.Parse(dr["Cost"].ToString()) + (decimal.Parse(tbNewByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1: //  under List price
                                    suggestedPrice = hla.listPrice - (decimal.Parse(tbNewByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2: //  above the average price
                                    suggestedPrice = hla.averagePrice + (decimal.Parse(tbNewByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                            }
                        else if (rbPriceNewPct.Checked == true)  //  use a percentage
                            switch (lbNewWhatPrice.SelectedIndex) {
                                case 0:  //  over cost
                                    suggestedPrice = decimal.Parse(dr["Cost"].ToString()) - decimal.Parse(dr["Cost"].ToString()) *
                                        (decimal.Parse(tbNewByAmt.Text) / 100);
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1:  //  under List price
                                    suggestedPrice = hla.listPrice - (hla.listPrice * (decimal.Parse(tbNewByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2:  //  above the average
                                    suggestedPrice = hla.averagePrice + (hla.averagePrice * (decimal.Parse(tbNewByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                            }
                    }
                }
                else  //  this is NOT a new item...
                {
                    //  if my price is high...
                    decimal decPrice = decimal.Parse(myPrice);
                    if (decPrice > hla.averagePrice)  //  are we higher than the average?
                    {
                        if (rbPriceHighFixed.Checked == true)
                            switch (lbWhatPriceH.SelectedIndex) {
                                case 0:  //  low
                                    //  check here to see if we subtract or add 
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.lowPrice - (decimal.Parse(tbHighByAmt.Text));
                                    else
                                        suggestedPrice = hla.lowPrice + (decimal.Parse(tbHighByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1:  //  average
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.averagePrice - (decimal.Parse(tbHighByAmt.Text));
                                    else
                                        suggestedPrice = hla.averagePrice + (decimal.Parse(tbHighByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2:  //  high
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.highPrice - (decimal.Parse(tbHighByAmt.Text));
                                    else
                                        suggestedPrice = hla.highPrice + (decimal.Parse(tbHighByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                default:
                                    break;
                            }
                        else if (rbPriceHighPct.Checked == true)
                            switch (lbWhatPriceH.SelectedIndex) {
                                case 0:  //  low
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.lowPrice - (hla.lowPrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.lowPrice + (hla.lowPrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1:  //  average
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.averagePrice - (hla.averagePrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.averagePrice + (hla.averagePrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2:  //  high
                                    if (rbHighBelow.Checked == true)
                                        suggestedPrice = hla.highPrice - (hla.highPrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.highPrice + (hla.highPrice * (decimal.Parse(tbHighByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                default:
                                    break;
                            }
                    }  //  else, my price must be low
                    else if (decPrice < hla.averagePrice) {
                        if (rbPriceLowFixed.Checked == true)
                            switch (lbWhatPriceL.SelectedIndex) {
                                case 0:  //  low
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.lowPrice - (decimal.Parse(tbLowByAmt.Text));
                                    else
                                        suggestedPrice = hla.lowPrice + (decimal.Parse(tbLowByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1:  //  average
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.averagePrice - (decimal.Parse(tbLowByAmt.Text));
                                    else
                                        suggestedPrice = hla.averagePrice + (decimal.Parse(tbLowByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2:  //  high
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.highPrice - (decimal.Parse(tbLowByAmt.Text));
                                    else
                                        suggestedPrice = hla.highPrice + (decimal.Parse(tbLowByAmt.Text));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                default:
                                    break;
                            }
                        else if (rbPriceLowPct.Checked == true)
                            switch (lbWhatPriceL.SelectedIndex) {
                                case 0:  //  low
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.lowPrice - (hla.lowPrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.lowPrice + (hla.lowPrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 1:  //  average
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.averagePrice - (hla.averagePrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.averagePrice + (hla.averagePrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                case 2:  //  high
                                    if (rbLowBelow.Checked == true)
                                        suggestedPrice = hla.highPrice - (hla.highPrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    else
                                        suggestedPrice = hla.highPrice + (hla.highPrice * (decimal.Parse(tbLowByAmt.Text) / 100));
                                    suggestedPrice = Math.Round(suggestedPrice, 2);
                                    break;
                                default:
                                    break;
                            }

                    }
                    else if (decPrice == hla.averagePrice)  //  my price is equal to average, low and high (I have the only item listed)
                    {
                        suggestedPrice = decPrice;
                    }
                }

                //  1st Edition processing
                if (tb1stPremium.Text.Length > 0)
                    if (dr["Ed"].ToString().Contains("First") || dr["Ed"].ToString().Contains("1st"))
                        suggestedPrice += suggestedPrice * (decimal.Parse(tb1stPremium.Text) / 100);

                //  Signed processing
                if (tbSignedPremium.Text.Length > 0)
                    if (dr["Signed"].ToString().Contains("A") || dr["Signed"].ToString().Contains("I"))
                        suggestedPrice += suggestedPrice * (decimal.Parse(tbSignedPremium.Text) / 100);

                //  Condition processing
                if (dr["Condn"].ToString().Length != 0 && tbCondAmtVG.Text.Length > 0 && dr["Condn"].ToString() == "Very Good")
                    suggestedPrice -= suggestedPrice * (decimal.Parse(tbCondAmtVG.Text) / 100);

                if (dr["Condn"].ToString().Length != 0 && tbCondAmtG.Text.Length > 0 && dr["Condn"].ToString() == "Good")
                    suggestedPrice -= suggestedPrice * (decimal.Parse(tbCondAmtG.Text) / 100);

                if (dr["Condn"].ToString().Length != 0 && tbCondAmtP.Text.Length > 0 && dr["Condn"].ToString() == "Poor")
                    suggestedPrice -= suggestedPrice * (decimal.Parse(tbCondAmtP.Text) / 100);

                ////  adjust for weight 
                //if (cbAdjustPostage.Checked == true && dr["BookWeight"].ToString() != "")
                //    suggestedPrice = suggestedPrice + adjustPostage(dr["BookWeight"].ToString());

                suggestedPrice = Math.Round(suggestedPrice, 2);  //  round it off...

                if (cbDontGoBelowCost.Checked == true && suggestedPrice < (decimal)dr["Cost"])  //  and it's less than our cost...
                    suggestedPrice = (decimal)dr["Cost"];  //  make it equal to our cost...

                if (cbDontGoBelowCost.Checked == true && tbBelowMyCostOr.Text.Length > 0)  //  and we have set a limit...
                {
                    //  if the suggested price is less than our cost and our cost is less than our limit...
                    if (suggestedPrice < decimal.Parse(tbBelowMyCostOr.Text) && (decimal)dr["Cost"] < decimal.Parse(tbBelowMyCostOr.Text))
                        suggestedPrice = decimal.Parse(tbBelowMyCostOr.Text.ToString());  //  set it to our limit...
                }

                dr.Close();  //  close the data reader

                return suggestedPrice;
            }
            return 0.00M;
        }


        //---------------    get total item count, UPCs and media type from database    ------------------------
        private void getMediaCountfromDB(List<sUPCList> UPCList, ArrayList parms) {
            FbCommand cmd = new FbCommand();
            FbDataReader dr = null;
            FbConnection mediaConn = (FbConnection)parms[0];
            RadioButton rbStartWithNbr = (RadioButton)parms[3];
            RadioButton rbRepriceThus = (RadioButton)parms[4];
            DateTimePicker dtpReprice = (DateTimePicker)parms[5];
            TextBox tbStartNbr = (TextBox)parms[6];
            CheckBox cbExcludeAbove = (CheckBox)parms[20];
            CheckBox cbExcludeBelow = (CheckBox)parms[21];
            TextBox tbExcludeAboveAmt = (TextBox)parms[22];
            TextBox tbExcludeBelowAmt = (TextBox)parms[23];
            TextBox tbBelowMyCostOr = (TextBox)parms[56];
            CheckBox cbDontGoBelowCost = (CheckBox)parms[29];

            if (rbStartWithNbr.Checked == true && tbStartNbr.Text.Length == 0)  //  start from indicated SKU
            {
                MessageBox.Show("You indicated you want to start with a specific SKU, but you did not provide one.", "Prager Media Inventory Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (cbDontGoBelowCost.Checked == true && tbBelowMyCostOr.Text.Length == 0) {
                MessageBox.Show("You indicated don't go below a certain price, but you did not provide one.", "Prager Media Inventory Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (cbExcludeAbove.Checked == true && tbExcludeAboveAmt.Text.Length == 0) {
                MessageBox.Show("You indicated exclude above a certain price, but you did not provide one.", "Prager Media Inventory Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (cbExcludeBelow.Checked == true && tbExcludeBelowAmt.Text.Length == 0) {
                MessageBox.Show("You indicated exclude Below a certain price, but you did not provide one.", "Prager Media Inventory Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Label pss = (Label)parms[2];

            string xxx = "SELECT UPC, SKU, Condn, Price FROM tMedia WHERE Stat = 'For Sale' AND UPC != '' " +
                    "AND extract(month from DateU) = '" + dtpReprice.Value.Month + "' AND extract(day from DateU) = '" +
                    dtpReprice.Value.Day + "' AND extract(year from DateU) = '" + dtpReprice.Value.Year + "' " +
                    "AND DoNotReprice != 'T' ORDER BY SKU  ASC";


            //  now get the UPCs and a limited amount of data
            if (rbStartWithNbr.Checked == true)  //  start from indicated SKU
                cmd = new FbCommand("SELECT UPC, SKU, Condn, Price FROM tMedia WHERE Stat = 'For Sale' AND UPC != '' AND SKU >= '" + tbStartNbr.Text.Trim() +
                    "' AND DoNotReprice != 'T' ORDER BY SKU ASC ", mediaConn);
            else if (rbRepriceThus.Checked == true)  //  only want to do today's entries => this date
                cmd = new FbCommand("SELECT UPC, SKU, Condn, Price FROM tMedia WHERE Stat = 'For Sale' AND UPC != '' " +
                    "AND extract(month from DateU) >= '" + dtpReprice.Value.Month + "' AND extract(day from DateU) >= '" +
                    dtpReprice.Value.Day + "' AND extract(year from DateU) >= '" + dtpReprice.Value.Year + "' " +
                    "AND DoNotReprice != 'T' ORDER BY SKU  ASC", mediaConn);
            else  //  start from beginning
                cmd = new FbCommand("SELECT UPC, SKU, Condn, Price FROM tMedia WHERE Stat = 'For Sale'AND UPC != '' AND DoNotReprice != 'T'  ORDER BY SKU ASC", mediaConn);

            pss.Text = "Obtaining list of UPCs...";
            pss.Refresh();

            try {
                if (mediaConn.State == ConnectionState.Closed)
                    mediaConn.Open();

                dr = cmd.ExecuteReader();

                decimal exclAbove = 0;
                decimal exclBelow = 0;
                if (cbExcludeAbove.Checked == true)
                    exclAbove = decimal.Parse(tbExcludeAboveAmt.Text);
                if (cbExcludeBelow.Checked == true)
                    exclBelow = decimal.Parse(tbExcludeBelowAmt.Text);

                while (dr.Read())  //  place all UPCs in the array, starting pos -> 1 (0 is count)
                {
                    //  look to see if we are supposed to process this item
                    if (cbExcludeAbove.Checked == true)
                        if (dr.GetDecimal(3) > exclAbove)
                            continue;

                    if (cbExcludeBelow.Checked == true)
                        if (dr.GetDecimal(3) < exclBelow)
                            continue;

                    sUPCList UPCs = new sUPCList();
                    UPCs.UPC = dr.GetString(0);  //  UPC
                    UPCs.SKU = dr.GetString(1);  //  get SKU while we're here
                    UPCs.mediaType = dr.GetString(2).ToString().ToLower().Contains("new") ? 'n' : 'u'; //  if condition is 'new'
                    UPCs.Price = dr.GetDecimal(3);
                    UPCList.Add(UPCs);  //  add it to the list...
                }
            }
            catch (Exception ex)  //  just in case return is null
            {
                if (ex.Message.Contains("Object reference not set to an instance of an object.")) {
                    MessageBox.Show("No items matched selection criteria", "Prager Media Inventory Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else {
                    MessageBox.Show("Database error: " + ex.Message + " Contact support@pragersoftware.com", "Prager Media Inventory Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            return;

        }


        internal decimal dCost;
        //--------------------    displays prices in the listview    ------------------------
        private void displayInListview(ArrayList parms, string SKU, highLowAverage hla, decimal sPrice) {
            string title = "";
            string UPC = "";
            decimal currentPrice = 0.00M;
            decimal cost = 0.00M;

            FbConnection mediaConn = ((FbConnection)parms[0]);
            ListView lvPricingService = (ListView)parms[7];
            CheckBox cbAboveLow = (CheckBox)parms[16];
            CheckBox cbAboveAverage = (CheckBox)parms[48];
            CheckBox cbAboveHigh = (CheckBox)parms[49];
            CheckBox cbBelowAverage = (CheckBox)parms[50];
            CheckBox cbGreaterSugg = (CheckBox)parms[51];
            CheckBox cbEqualSugg = (CheckBox)parms[52];
            CheckBox cbLessSugg = (CheckBox)parms[53];

            //  get display information from d/b
            FbCommand cmd = new FbCommand("SELECT * FROM tMedia WHERE SKU = '" + SKU + "' ", mediaConn);
            FbDataReader dr = cmd.ExecuteReader();
            while (dr.Read()) {
                title = (string)dr[1];
                UPC = (string)dr[3];
                currentPrice = (decimal)dr[6];
                cost = (decimal)dr[7];
            }
            dr.Close();

            ListViewItem lvi = new ListViewItem(SKU);
            lvi.UseItemStyleForSubItems = false;  //  this allows me to color items

            lvi.SubItems.Add(UPC);
            lvi.SubItems.Add(title);
            lvi.SubItems.Add(currentPrice.ToString("C"));

            //  put results in listview (lvPricingService)
            if (hla.averagePrice == 0) {
                lvi.SubItems.Add("---");  //  low price
                lvi.SubItems.Add("---");  //  average price
                lvi.SubItems.Add("---");  //  high price
            }
            else {
                lvi.SubItems.Add(hla.lowPrice.ToString("C").Replace("$", ""));  //  low price
                lvi.SubItems.Add(hla.averagePrice.ToString("C").Replace("$", ""));  //  average price
                lvi.SubItems.Add(hla.highPrice.ToString("C").Replace("$", ""));  //  high price
            }

            //  display cost
            string myCost = cost.ToString();
            dCost = decimal.Parse(myCost);
            dCost = Math.Round(dCost, 2);
            lvi.SubItems.Add(dCost.ToString("C").Replace("$", ""));

            if (hla.averagePrice != 0)
                lvi.SubItems.Add(sPrice.ToString("C").Replace("$", ""));
            else
                lvi.SubItems.Add("---");  //  suggested price

            if (cbAboveLow.Checked == true && currentPrice > hla.lowPrice)  //  my price > low
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbAboveAverage.Checked == true && currentPrice > hla.averagePrice)  //  my price > average
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbAboveHigh.Checked == true && currentPrice > hla.highPrice)  //  my price > high
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbBelowAverage.Checked == true && currentPrice < hla.averagePrice)  //  my price < average
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbGreaterSugg.Checked == true && currentPrice > sPrice)
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbEqualSugg.Checked == true && currentPrice == sPrice)
                lvi.SubItems[3].ForeColor = Color.Red;
            else if (cbLessSugg.Checked == true && currentPrice < sPrice)
                lvi.SubItems[3].ForeColor = Color.Red;

            lvPricingService.Tag = "Title";
            lvPricingService.Items.Add(lvi);  // Add the list items to the ListView
        }


    //    //-----------------------    adjust postage based on weight over 2 lbs    ---------------------------------
    //    private decimal adjustPostage(string bookWeight) {
    //        //  determine how much to add
    //        decimal bkWt = decimal.Parse(bookWeight);
    //        bkWt = bkWt * 16;  //  convert lbs to ounces...

    //        if (bkWt < 33.0M)
    //            return 0.00M;  //  less than 2 lbs, return 0
    //        else if (bkWt > 32.0M && bkWt < 49M)
    //            return 0.48M;  //  between 2-3 lbs, return .48
    //        else if (bkWt > 48.0M && bkWt < 65M)
    //            return 0.96M;  //  between 3-4 lbs
    //        else if (bkWt > 64.0M && bkWt < 81M)
    //            return 1.44M;  //  between 4-5 lbs
    //        else if (bkWt > 80.0M && bkWt < 97M)
    //            return 1.92M;  //  between 5-6 lbs
    //        else if (bkWt > 96.0M && bkWt < 113M)
    //            return 2.40M;  //  between 6-7 lbs
    //        else if (bkWt > 112.0M && bkWt < 129M)
    //            return 2.74M;  //  between 7-8 lbs
    //        else if (bkWt > 128.0M && bkWt < 145M)
    //            return 3.08M;  //  between 8-9 lbs
    //        else if (bkWt > 144.0M)
    //            return 3.42M;  //  over 9lbs
    //        else return 0.00M;
    //    }
    }
}