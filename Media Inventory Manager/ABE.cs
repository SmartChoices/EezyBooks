﻿using System;
using System.IO;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;

namespace Media_Inventory_Manager
{

    partial class mainForm : Form
    {

        //      TextWriter tw1;

        //------------------------    create a file in tab-delimited format for EXPORT   -------------------------------
        public int createABEExport(string formattedDate) {

            Cursor.Current = Cursors.WaitCursor;

            if (rbExportAll.Checked == false && rbChangeDate.Checked == false && rbExportInclusiveSearch.Checked == false && rbExportSelected.Checked == false) {
                MessageBox.Show("You must choose 'All' or 'Changed since' for Export", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }

#if EXPORTTESTING
                exportPath = @"d:\temp\prager\export\";  //  create normal export file
#endif
            //if (cbPurgeReplace.Checked == true)  //  doing a purge/replace?
            //    sFileName1 = exportPath + "purgeTB" + formattedDate + ".tab";
            //else  //  no just exporting those that were changed
            sFileName1 = exportPath + "ABE" + formattedDate + ".tab";

            try {
                tw1 = new StreamWriter(sFileName1);  //  for normal tab-delimited files
            }
            catch (Exception e) {
                if (e.ToString().Substring(10, 26) == "DirectoryNotFoundException") {
                    try {
                        DirectoryInfo di = Directory.CreateDirectory(exportPath);   // Try to create the directory
                        tw1 = new StreamWriter(sFileName1);
                    }
                    catch (Exception) {
                        lbStatus.Items.Insert(0, "Error - unable to create export directory");
                        lbStatus.Refresh();
                        return -1;
                    }
                }
            }

            createExportCommandString();

            //  find books in table
            FbCommand command = new FbCommand(commandString, databaseConn);
            FbDataReader data = command.ExecuteReader();

            int count = 0;
            lbUploadStatus.Items.Insert(0, "ABE format export started");
            lbUploadStatus.Refresh();

            //  write header
            tw1.WriteLine("listingid\t" +
                "title\t" +
                "author\t" +
                "illustrator\t" +
                "price\t" +
                "quantity\t" +
                "booktype\t" +
                "description\t" +
                "bindingtext\t" +
                "bookcondition\t" +
                "publishername\t" +
                "placepublished\t" +
                "yearpublished\t" +
                "UPC\t" +
                "sellercatalog1\t" +
                "sellercatalog2\t" +
                "abecategory\t" +
                "keywords\t" +
                "jacketcondition\t" +
                "editiontext\t" +
                "printingtext\t" +
                "signedtext\t" +
                "volume\t" +
                "size\t");

            while (data.Read()) {
                tbSKU.Text = data["SKU"].ToString();  //  for debugging purposes only!

                if (cbPurgeReplace.Checked == true && data["Stat"].ToString() == "Sold")
                    continue;  //  if purge/replace and Sold, don't upload

                buildABETabDelimitedFile(data);

                count++;  //  increment counter
            }

            tw1.Close();  //  close the stream

            if (data != null)  //  close the data reader
                data.Close();

            Cursor.Current = Cursors.Default;

            lbUploadStatus.Items.Insert(0, "ABE format export(s) completed: " + count + " books exported to file " + sFileName1);
            lbUploadStatus.Refresh();

            return 0;
        }


        //-----------------------    build a TAB delimited format file    --------------------------------------
        private void buildABETabDelimitedFile(FbDataReader data) {

            string dataBuild;

            dataBuild = data["SKU"].ToString() + "\t";  //  book number (req'd)

            dataBuild += data["Title"].ToString() + "\t";  //  title  (req'd)

            dataBuild += data["Author"].ToString() + "\t";  //  author

            if (data["Illus"]  != DBNull.Value)
                dataBuild += data["Illus"].ToString() + "\t";  //  illustrator
            else
                dataBuild += " \t";

            dataBuild += data["Price"] + "\t";  //  price  (req'd)

            dataBuild += data["Quantity"] + "\t";  //  quantity  (req'd)

            dataBuild += "book\t";  //  booktype

            if (data["Descr"] != DBNull.Value) {  //  description  (req'd)
                //dataBuild += cbIntlStd.Checked == true ? "Will ship international, " : "Will NOT ship international; ";
                dataBuild += data["Descr"].ToString() + "\t";
            }
            else
                dataBuild += "see condition\t";  //  default

            if (data["Bndg"] != DBNull.Value)
                dataBuild += data["Bndg"].ToString() + "\t";  //  binding  (req'd)
            else
                dataBuild += " Softcover\t";  //  default if missing

            string tempCond = "";
            if (data["Condn"] != DBNull.Value)  //  condition (req'd)  11.3.10
            {
                tempCond = data["Condn"].ToString();
                if (tempCond.ToLower() == "new")
                    dataBuild += "New\t";
                else if (tempCond.ToLower().Contains("used") || tempCond.ToLower().Contains("collectible")) {
                    if (tempCond.ToLower().Contains("new"))
                        dataBuild += "As New\t";
                    else if (tempCond.ToLower().Contains("fine"))
                        dataBuild += "Fine\t";
                    else if (tempCond.ToLower().Contains("very good"))
                        dataBuild += "Very Good\t";
                    else if (tempCond.ToLower().Contains("good"))
                        dataBuild += "Good\t";
                    else if (tempCond.ToLower().Contains("fair"))
                        dataBuild += "Fair\t";
                    else if (tempCond.ToLower().Contains("poor"))
                        dataBuild += "Poor\t";
                    else
                        dataBuild += "Good\t";  //  default: Good
                }
            }

            if (data["Pub"] != DBNull.Value)
                dataBuild += data["Pub"].ToString() + "\t";  //  Mfgr
            else
                dataBuild += " \t";

            if (data["PubPlace"] != DBNull.Value)
                dataBuild += data["PubPlace"].ToString() + "\t";  //  place published
            else
                dataBuild += " \t";

            if (data["PubYear"] != DBNull.Value)
                dataBuild += data["PubYear"].ToString() + "\t";  //  year published
            else
                dataBuild += " \t";

            if (data["UPC"].ToString().StartsWith("B"))  //  UPC
                dataBuild += data["UPC"].ToString() + "\t";
            else
                dataBuild += " \t";  //  leave it blank if it's blank or an ASIN

            if (data["Cat"] != DBNull.Value)
                dataBuild += data["Cat"].ToString() + "\t";  //  catalog
            else
                dataBuild += " \t";

            if (data["SubCategory"] != DBNull.Value)  //  sub-catalog
                dataBuild += data["SubCategory"].ToString() + "\t";  //  sub-catalog
            else
                dataBuild += " \t";

            dataBuild += " \t";  //  ABE category

            if (data["Keywds"] != DBNull.Value)
                dataBuild += data["Keywds"].ToString() + "\t";  //  keywords
            else
                dataBuild += " \t";

            if (data["Jaket"] != DBNull.Value)
                dataBuild += data["Jaket"].ToString() + "\t";  //  jacket
            else
                dataBuild += " \t";

            if (data["Ed"] != DBNull.Value)
                dataBuild += data["Ed"].ToString() + "\t";  //  Edition
            else
                dataBuild += " \t";

            dataBuild += " \t";  //  printing text

            if (data["Signed"] != DBNull.Value)  //  signed-by         
                if (data["Signed"].ToString() == "B")
                    dataBuild += "Signed by Author and Illustrator\t";
                else if (data["Signed"].ToString() == "A")
                    dataBuild += "Signed by Author\t";
                else if (data["Signed"].ToString() == "I")
                    dataBuild += "Signed by Illustrator\t";
                else
                    dataBuild += " \t";

            dataBuild += " \t";  //  volume

            if (data["BookSize"] != DBNull.Value) {
                string[] tempSize = data["BookSize"].ToString().Split(' ');
                dataBuild += tempSize[1] + tempSize[2] + tempSize[3] + tempSize[4] + "\t";  //  book size
            }
            else
                dataBuild += " \t";

            tw1.WriteLine(dataBuild);  //  write it out...
            return;
        }
    }
}
