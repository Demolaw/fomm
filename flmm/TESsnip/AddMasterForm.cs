﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Fomm.TESsnip
{
	public partial class AddMasterForm : Form
	{
		public string MasterName
		{
			get
			{
				return tbxMaster.Text;
			}
		}

		public AddMasterForm()
		{
			InitializeComponent();
			ofdChooseMaster.InitialDirectory = Path.GetFullPath("data");
		}

		private void butOK_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
		}

		private void butChooseMaster_Click(object sender, EventArgs e)
		{
			if (ofdChooseMaster.ShowDialog() == DialogResult.OK)
				tbxMaster.Text = Path.GetFileName(ofdChooseMaster.FileName);
		}
	}
}
