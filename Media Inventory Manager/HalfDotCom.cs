//#define EXPORTTESTING  //  TESTING ONLY  <-----------------

#region Using directives

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;

#endregion


namespace Media_Inventory_Manager
{

    //------------    eBay/Half.com Sandbox keys    --------------------]
    //    DEVID:  0a72c1d0-ddfd-4ccc-9bef-b3814bd2644d
    //    AppID:  PragerSo-3252-4f56-a1ac-3343dfdb0c79
    //    CertID:  99bf2489-c272-4315-b2a2-3970ecd501cf

    //----------    eBay/Half.com Production keys    -------------------]
    //    DEVID:  0a72c1d0-ddfd-4ccc-9bef-b3814bd2644d
    //    AppID:  PragerSo-5527-4bbe-bb3b-a158cd64ebc2
    //    CertID:  cc53a4de-ba12-4f6f-8284-5035799d0209   



    partial class mainForm : Form
    {

        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //--   create a file in comma-separated-value format for Half.com export
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        public int createHalfDotComCSVExportFile(string formattedDate) {
            Cursor.Current = Cursors.WaitCursor;

            if (rbExportAll.Checked == false && rbChangeDate.Checked == false && rbExportInclusiveSearch.Checked == false && rbExportSelected.Checked == false) {
                MessageBox.Show("You must choose 'All' or 'Changed since' for Export", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }

            createHDCFilename(formattedDate);
            createExportCommandString();

            //  find items in table
            FbCommand command = new FbCommand(commandString, mediaConn);

            int count = 0;
            lbUploadStatus.Items.Insert(0, "Half.com format export started");
            lbUploadStatus.Refresh();

            if (cbPurgeReplace.Checked == true)  //  doing a purge/replace?  
            {
                FbDataReader dr = command.ExecuteReader();
                createPurgeListing(dr);
                dr.Close();
            }

            FbDataReader data = command.ExecuteReader();
            while (data.Read()) {
                tbSKU.Text = data.GetString(0);  //  for debugging purposes only!

                if (cbPurgeReplace.Checked == true && data.GetString(25) == "Sold")
                    continue;  //  if purge/replace and Sold, don't upload

                if (data.GetString(25) == "Hold")
                    continue;  //  don't export

                if (data["UPC"].ToString().Length < 10 || data["UPC"].ToString().StartsWith("B"))  //  can only take items with UPCs (no ASINs)
                    continue;

                buildHalfDotComCSVFile(data);  //  now, build a record...
                count++;  //  increment counter
            }

            tw1.Close();  //  close the stream

            if (data != null)  //  close the data reader
                data.Close();

            Cursor.Current = Cursors.Default;

            lbUploadStatus.Items.Insert(0, "Half.com format export(s) completed: " + count + " items exported to file " + sFileName1);
            lbUploadStatus.Refresh();

            return 0;
        }


        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //--    create "end" listings for purge/replace
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void createPurgeListing(FbDataReader dr) {

            while (dr.Read()) {
                string dataBuild = "";

                //  action and productIDType
                if (dr["Stat"].ToString() == "For Sale" && dr["UPC"].ToString().Length != 0) {
                    dataBuild = "End, UPC,";  //  action and productIDType
                    dataBuild += dr["UPC"].ToString() + ", ";  //  ProductIDValue
                    dataBuild += ", ";  //  ItemID (blank)
                    dataBuild += dr["SKU"].ToString() + ", ";  //  SKU
                    tw1.WriteLine(dataBuild);
                }
            }
        }


        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //--    creates HDC filename for when there are over 1000 records
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private int createHDCFilename(string formattedDate) {
            //if (seqNumber > 0)
            //    tw1.Close();  //  close file if we are going to create next file

            if (cbPurgeReplace.Checked == true)  //  doing a purge/replace?
                sFileName1 = exportPath + "purgeHDC" + formattedDate + ".csv";
            else  //  no just exporting those that were changed
                sFileName1 = exportPath + "HDC" + formattedDate + ".csv";

            try  //  look for Export directory
            {
                tw1 = new StreamWriter(sFileName1);
                tw1.WriteLine("Action (SiteID=US|Country=US|Currency=USD|CC=UTF-8|ListingType=Half|Location=US|ListingDuration=GTC)," +
                    "ProductIDType,ProductIDValue,ItemID,SKU,NbrOfCopies,Price,A:Condition,A:Notes");

                return 0;
            }
            catch (Exception e) {
                if (e.ToString().Substring(10, 26) == "DirectoryNotFoundException") {
                    try {
                        DirectoryInfo di = Directory.CreateDirectory(exportPath);   // Try to create the directory
                        tw1 = new StreamWriter(sFileName1);

                        //  write header (Half.com Metadata)                         
                        tw1.WriteLine("Action (SiteID=US|Country=US|Currency=USD|CC=UTF-8|ListingType=Half|Location=US|ListingDuration=GTC)," +
                            "ProductIDType,ProductIDValue,ItemID,SKU,NbrOfCopies,Price,A:Condition,A:Notes,PrivateNotes");
                    }
                    catch (Exception e1) {
                        MessageBox.Show("Unable to create export directory: " + e1.Message, "Prager Media Inventory Manager",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return -1;
                    }
                }

                return 0;
            }
        }



        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //--    build the actual file to be uploaded
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void buildHalfDotComCSVFile(FbDataReader data) {

            string workingDescrField = "";
            /*
            Action (SiteID=US|Country=US|Currency=USD|CC=UTF-8|ListingType=Half|Location=US|ListingDuration=GTC),
            ProductIDType,ProductIDValue,ItemID,SKU,Quantity,Price,A:Condition,A:Notes,PrivateNotes
            * */

            string dataBuild = "";

            //  action and productIDType
            if (data["Stat"].ToString() == "For Sale") {

                if (cbPurgeReplace.Checked == true)  //  if doing a purge, can only Add
                    dataBuild = "Add, UPC,";  //  action and productIDType
                else if (data["TranC"].ToString() == "A")
                    dataBuild = "Add, UPC,";  //  action and productIDType
                else if (data["TranC"].ToString() != "A" && int.Parse(data["Quantity"].ToString()) == 0) //  end...
                    dataBuild = "End, UPC,";  // just revising prices or something
                else if (data["TranC"].ToString() != "A" && int.Parse(data["Quantity"].ToString()) > 0) //  revise...
                    dataBuild = "Revise, UPC,";  // just revising prices or something

                string tempUPC = data["UPC"].ToString().StartsWith("B") ? " " : data["UPC"].ToString();
                dataBuild += tempUPC + ", ";  //  ProductIDValue

                dataBuild += ", ";  //  ItemID (blank)
                dataBuild += data["SKU"].ToString() + ", ";  //  SKU
                dataBuild += data["Quantity"].ToString() + ", ";  //  Quantity
                dataBuild += data["Price"].ToString() + ", ";  // price

                //----    Amazon.com conditions    ----
                //* 1 = Used; Like New
                //* 2 = Used; Very Good
                //* 3 = Used; Good
                //* 4 = Used; Acceptable
                //* 5 = Collectible; Like New
                //* 6 = Collectible; Very Good
                //* 7 = Collectible; Good
                //* 8 = Collectible; Acceptable
                //* 11 = New

                //Half.com conditions are:  Brand_New, Like_New, Very_Good, Good, or Acceptable
                if (data["Condn"] != DBNull.Value)  //  condition
                    switch (data["Condn"].ToString().ToLower()) {
                        case "new":
                            dataBuild += "Brand_New, ";
                            break;
                        case "fine":
                        case "fine - used":
                        case "fine - collectible":
                        case "used: like new":
                        case "collectible: like new":
                            dataBuild += "Like_New, ";
                            break;
                        case "very good":
                        case "very good - used":
                        case "very good - collectible":
                        case "used: very good":
                        case "collectible: very good":
                            dataBuild += "Very_Good, ";
                            break;
                        case "good":
                        case "good - used":
                        case "good - collectible":
                        case "used: good":
                        case "collectible: good":
                            dataBuild += "Good, ";
                            break;
                        case "poor":
                        case "poor - used":
                        case "poor - collectible":
                        case "fair":
                        case "fair - used":
                        case "fair - collectible":
                        default:
                            dataBuild += "Acceptable, ";
                            break;
                    }

                if (data["Descr"] != DBNull.Value) {  //  description (notes)
                    workingDescrField = data["Descr"].ToString();
                    workingDescrField = workingDescrField.Replace("\"", "in. ");  //  replace " with the word 'in.'
                    dataBuild += workingDescrField.Replace(',', ';');  //  replace commas with semi-colons
                }
            }
            else if (data["Stat"].ToString() == "Sold") {
                dataBuild = "End,,,,";  // item is sold
                dataBuild += data["SKU"].ToString();  // +", ";  //  ProductIDValue
            }

            tw1.WriteLine(dataBuild);

            return;
        }


        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //--    upload the file to half.com
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void doHalfDotComUpload(string sFileName, string token) {

            //  check token for expiration  <-------------------------  TODO

            const string boundary = "THIS_STRING_SEPARATES";
            const string newLine = "\r\n";
            //  token: AgAAAA**AQAAAA**aAAAAA**uYiHRw**nY+sHZ2PrBmdj6wVnY+sEZ2PrA2dj6wJnYuhDpaLoAmdj6x9nY+seQ**DAsAAA**AAMAAA**y9/8TcAmm8gCijqwhSxxWsHg3QjnRqpzB+LbUldfNMOvWQVUPHSoPEYFPa0QXm/Ekgg5cUvKg8RRxHC3swHT+RSG6w144BcSbc4GjPdJ7Z7oOO84qwUJwb1Ufxk9dqSxxpusnSmqjF8vxDsD7ovqzAaRsqhZGhriy8ZeuymnnHW5pufSvMD9ZAM/MgdlJ45NM3O+eQPXuez2gh4oHraTx1R0Akg4RtKzxgjQl4lLfp3r9IGM0lh2ZP56PnYfdDAMdKAbcIyPo3tN86tCCqZabWNZuRY5UQDaQbBEySyLb0/FUGzwq9AhMSxAKI0hX0wZK//PTuKFUoZIDrIDYT21Bj7bUqGo9OAnMV2NSkA+Zivchd3vCDrHE+Ebi0CoFKa2aGksnE7pXMjVC3NeGC3TIE5vKf7AnpqCBbDJe+SpQSXU+aIxPgkaU7GAUH+kn2ONJZO77BKxIR75/nB+vw/LIUZAKOI8TH1c3NvXxLszm3Sed+x4dE6nyPlNAR+BypaWgPpwqU2x6HEZjYb/WYC9SNWDmNkZJUi/xluAiagZvi4eDG+JwGxsEbw6/dHGCQ3rrk1e2NZNdiEwVtPOqlx/f99HM1Gd34AUf08yA+t9ztapul+nzAdsna4V4zdIrYmjAbXeaaYkTNpMDQzxOIvmWLcAXjXI1AOoPQHUSRTh3Q+fJ7DJukyRgT9AOnKd1kiLa0Z1ni0ucEH1XRYK9k7zLnXFISCOIBro5Y4Mkl7h4MkPWMNh728zIES22QCtmW8l 

            if (tbHalfToken.Text.Length < 50) {
                MessageBox.Show("You are missing the Half.com token; see the Help file for details", "Prager Media Inventory Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Cursor.Current = Cursors.WaitCursor;

            //  create filename and read file into a stream
            string fileToUpload = sFileName.Replace("CSV", "HDC");  //  change filename so we can find it...
            //fileToUpload = fileToUpload.Replace(".txt", ".csv");
            string[] filenameArray = fileToUpload.Split('\\');  //  get actual filename for message...

            lbUploadStatus.Items.Insert(0, "starting transfer of " + filenameArray[3] + " to Half.com");
            lbUploadStatus.Refresh();

            StringBuilder dataStream = new StringBuilder();
            dataStream.Append("--" + boundary + newLine);
            dataStream.Append("Content-Disposition: form-data; name=\"token\"" + newLine + newLine);
            dataStream.Append(token + newLine);
            dataStream.Append("--" + boundary + newLine);
            dataStream.Append("Content-disposition: form-data; name=\"file\"; filename=\"" + fileToUpload + "\"" + newLine);
            dataStream.Append(@"Content-type: text/plain" + newLine + newLine);

            using (StreamReader sr = new StreamReader(fileToUpload)) {
                String line;
                while ((line = sr.ReadLine()) != null)
                    dataStream.Append(line + "\n");  //  read entire file into "dataStream"
            }
            dataStream.Append(newLine);
            dataStream.Append("--" + boundary + newLine);

            //  a single file can not be larger than 15 Mb (15,360,000 bytes) or 1000 items   <----------- TODO
            if (dataStream.Length > 15359999) {
                //  break file into two parts  <--------------- TODO
            }

            //  now build the WebRequest...
            HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(@"https://bulksell.ebay.com/ws/eBayISAPI.dll?FileExchangeUpload");
            webReq.Method = "POST";
            webReq.ContentType = @"multipart/form-data; boundary=" + boundary;
            webReq.UserAgent = "Prager Inventory Program v1.0.0";
            webReq.KeepAlive = true;
            webReq.ProtocolVersion = HttpVersion.Version10;

            byte[] postBuffer = System.Text.Encoding.UTF8.GetBytes(dataStream.ToString());
            webReq.ContentLength = postBuffer.Length;

            //  post the data
            Stream postDataStream = webReq.GetRequestStream();
            postDataStream.Write(postBuffer, 0, postBuffer.Length);
            postDataStream.Flush();
            postDataStream.Close();

            // Get the response
            HttpWebResponse response = (HttpWebResponse)webReq.GetResponse();
            Encoding enc = System.Text.Encoding.UTF8;   // Read the response from the stream
            StreamReader responseStream = new StreamReader(response.GetResponseStream(), enc, true);
            string responseHtml = responseStream.ReadToEnd();
            response.Close();
            responseStream.Close();

            //  validate response  <---------------------------------------------------- TODO
            lbUploadStatus.Items.Insert(0, "upload to Half.com ended successfully");
            lbUploadStatus.Refresh();

            Cursor.Current = Cursors.Default;
        }
    }
}


