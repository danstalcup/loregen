namespace Worldbuilder
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.climatesTab = new System.Windows.Forms.TabPage();
            this.label5 = new System.Windows.Forms.Label();
            this.biomeTextBox = new System.Windows.Forms.RichTextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.terrainTextBox = new System.Windows.Forms.RichTextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.climateTextBox = new System.Windows.Forms.RichTextBox();
            this.testContTab = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.regionsTextBox = new System.Windows.Forms.RichTextBox();
            this.testContInfoBox = new System.Windows.Forms.RichTextBox();
            this.langTestTab = new System.Windows.Forms.TabPage();
            this.button1 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.testMapTab = new System.Windows.Forms.TabPage();
            this.mapBackButton = new System.Windows.Forms.Button();
            this.currentBlockInfo = new System.Windows.Forms.RichTextBox();
            this.button4 = new System.Windows.Forms.Button();
            this.contextListBox = new System.Windows.Forms.ListBox();
            this.newWorldButton = new System.Windows.Forms.Button();
            this.mapTest = new System.Windows.Forms.Panel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.currentContinentTab = new System.Windows.Forms.TabPage();
            this.mapTestContinent = new System.Windows.Forms.Panel();
            this.climatesTab.SuspendLayout();
            this.testContTab.SuspendLayout();
            this.langTestTab.SuspendLayout();
            this.testMapTab.SuspendLayout();
            this.tabs.SuspendLayout();
            this.currentContinentTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // climatesTab
            // 
            this.climatesTab.Controls.Add(this.label5);
            this.climatesTab.Controls.Add(this.biomeTextBox);
            this.climatesTab.Controls.Add(this.label4);
            this.climatesTab.Controls.Add(this.terrainTextBox);
            this.climatesTab.Controls.Add(this.label3);
            this.climatesTab.Controls.Add(this.climateTextBox);
            this.climatesTab.Location = new System.Drawing.Point(4, 22);
            this.climatesTab.Name = "climatesTab";
            this.climatesTab.Padding = new System.Windows.Forms.Padding(3);
            this.climatesTab.Size = new System.Drawing.Size(1044, 690);
            this.climatesTab.TabIndex = 3;
            this.climatesTab.Text = "Raw Climate Data";
            this.climatesTab.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(337, 7);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "Biomes";
            // 
            // biomeTextBox
            // 
            this.biomeTextBox.Location = new System.Drawing.Point(337, 27);
            this.biomeTextBox.Name = "biomeTextBox";
            this.biomeTextBox.Size = new System.Drawing.Size(325, 222);
            this.biomeTextBox.TabIndex = 4;
            this.biomeTextBox.Text = "";
            this.biomeTextBox.WordWrap = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 324);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(45, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Terrains";
            // 
            // terrainTextBox
            // 
            this.terrainTextBox.Location = new System.Drawing.Point(9, 343);
            this.terrainTextBox.Name = "terrainTextBox";
            this.terrainTextBox.Size = new System.Drawing.Size(423, 222);
            this.terrainTextBox.TabIndex = 2;
            this.terrainTextBox.Text = "";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 7);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(46, 13);
            this.label3.TabIndex = 1;
            this.label3.Text = "Climates";
            // 
            // climateTextBox
            // 
            this.climateTextBox.Location = new System.Drawing.Point(6, 27);
            this.climateTextBox.Name = "climateTextBox";
            this.climateTextBox.Size = new System.Drawing.Size(325, 222);
            this.climateTextBox.TabIndex = 0;
            this.climateTextBox.Text = "";
            // 
            // testContTab
            // 
            this.testContTab.Controls.Add(this.label2);
            this.testContTab.Controls.Add(this.label1);
            this.testContTab.Controls.Add(this.regionsTextBox);
            this.testContTab.Controls.Add(this.testContInfoBox);
            this.testContTab.Location = new System.Drawing.Point(4, 22);
            this.testContTab.Name = "testContTab";
            this.testContTab.Padding = new System.Windows.Forms.Padding(3);
            this.testContTab.Size = new System.Drawing.Size(1044, 690);
            this.testContTab.TabIndex = 2;
            this.testContTab.Text = "World Info";
            this.testContTab.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(437, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Regions";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 28);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Continents";
            // 
            // regionsTextBox
            // 
            this.regionsTextBox.Location = new System.Drawing.Point(437, 47);
            this.regionsTextBox.Name = "regionsTextBox";
            this.regionsTextBox.Size = new System.Drawing.Size(425, 278);
            this.regionsTextBox.TabIndex = 1;
            this.regionsTextBox.Text = "";
            // 
            // testContInfoBox
            // 
            this.testContInfoBox.Location = new System.Drawing.Point(6, 47);
            this.testContInfoBox.Name = "testContInfoBox";
            this.testContInfoBox.Size = new System.Drawing.Size(425, 278);
            this.testContInfoBox.TabIndex = 0;
            this.testContInfoBox.Text = "";
            // 
            // langTestTab
            // 
            this.langTestTab.Controls.Add(this.button1);
            this.langTestTab.Controls.Add(this.button3);
            this.langTestTab.Controls.Add(this.richTextBox1);
            this.langTestTab.Controls.Add(this.button2);
            this.langTestTab.Location = new System.Drawing.Point(4, 22);
            this.langTestTab.Name = "langTestTab";
            this.langTestTab.Padding = new System.Windows.Forms.Padding(3);
            this.langTestTab.Size = new System.Drawing.Size(1044, 690);
            this.langTestTab.TabIndex = 0;
            this.langTestTab.Text = "Language Generator";
            this.langTestTab.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(6, 249);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 39);
            this.button1.TabIndex = 1;
            this.button1.Text = "New Language";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(170, 249);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 39);
            this.button3.TabIndex = 3;
            this.button3.Text = "100 Names";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(6, 6);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(259, 236);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(88, 249);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 39);
            this.button2.TabIndex = 2;
            this.button2.Text = "Language Info";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // testMapTab
            // 
            this.testMapTab.Controls.Add(this.mapBackButton);
            this.testMapTab.Controls.Add(this.currentBlockInfo);
            this.testMapTab.Controls.Add(this.button4);
            this.testMapTab.Controls.Add(this.contextListBox);
            this.testMapTab.Controls.Add(this.newWorldButton);
            this.testMapTab.Controls.Add(this.mapTest);
            this.testMapTab.Location = new System.Drawing.Point(4, 22);
            this.testMapTab.Name = "testMapTab";
            this.testMapTab.Padding = new System.Windows.Forms.Padding(3);
            this.testMapTab.Size = new System.Drawing.Size(1044, 690);
            this.testMapTab.TabIndex = 1;
            this.testMapTab.Text = "Map";
            this.testMapTab.UseVisualStyleBackColor = true;
            // 
            // mapBackButton
            // 
            this.mapBackButton.Enabled = false;
            this.mapBackButton.Location = new System.Drawing.Point(903, 105);
            this.mapBackButton.Name = "mapBackButton";
            this.mapBackButton.Size = new System.Drawing.Size(58, 23);
            this.mapBackButton.TabIndex = 5;
            this.mapBackButton.Text = "Back";
            this.mapBackButton.UseVisualStyleBackColor = true;
            this.mapBackButton.Click += new System.EventHandler(this.mapBackButton_Click);
            // 
            // currentBlockInfo
            // 
            this.currentBlockInfo.Location = new System.Drawing.Point(822, 154);
            this.currentBlockInfo.Margin = new System.Windows.Forms.Padding(2);
            this.currentBlockInfo.Name = "currentBlockInfo";
            this.currentBlockInfo.Size = new System.Drawing.Size(205, 453);
            this.currentBlockInfo.TabIndex = 4;
            this.currentBlockInfo.Text = "";
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(822, 131);
            this.button4.Margin = new System.Windows.Forms.Padding(2);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(104, 19);
            this.button4.TabIndex = 3;
            this.button4.Text = "Change Definition";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // contextListBox
            // 
            this.contextListBox.FormattingEnabled = true;
            this.contextListBox.Location = new System.Drawing.Point(822, 7);
            this.contextListBox.Name = "contextListBox";
            this.contextListBox.Size = new System.Drawing.Size(120, 95);
            this.contextListBox.TabIndex = 2;
            this.contextListBox.SelectedIndexChanged += new System.EventHandler(this.contextListBox_SelectedIndexChanged_1);
            // 
            // newWorldButton
            // 
            this.newWorldButton.Location = new System.Drawing.Point(822, 107);
            this.newWorldButton.Margin = new System.Windows.Forms.Padding(2);
            this.newWorldButton.Name = "newWorldButton";
            this.newWorldButton.Size = new System.Drawing.Size(76, 19);
            this.newWorldButton.TabIndex = 1;
            this.newWorldButton.Text = "New World";
            this.newWorldButton.UseVisualStyleBackColor = true;
            this.newWorldButton.Click += new System.EventHandler(this.newWorldButton_Click);
            // 
            // mapTest
            // 
            this.mapTest.Location = new System.Drawing.Point(7, 7);
            this.mapTest.Name = "mapTest";
            this.mapTest.Size = new System.Drawing.Size(800, 600);
            this.mapTest.TabIndex = 0;
            this.mapTest.Click += new System.EventHandler(this.mapTest_Click);
            this.mapTest.DoubleClick += new System.EventHandler(this.mapTest_DoubleClick);
            // 
            // tabs
            // 
            this.tabs.Controls.Add(this.testMapTab);
            this.tabs.Controls.Add(this.testContTab);
            this.tabs.Controls.Add(this.langTestTab);
            this.tabs.Controls.Add(this.climatesTab);
            this.tabs.Controls.Add(this.currentContinentTab);
            this.tabs.Location = new System.Drawing.Point(13, 13);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(1052, 716);
            this.tabs.TabIndex = 4;
            // 
            // currentContinentTab
            // 
            this.currentContinentTab.Controls.Add(this.mapTestContinent);
            this.currentContinentTab.Location = new System.Drawing.Point(4, 22);
            this.currentContinentTab.Name = "currentContinentTab";
            this.currentContinentTab.Padding = new System.Windows.Forms.Padding(3);
            this.currentContinentTab.Size = new System.Drawing.Size(1044, 690);
            this.currentContinentTab.TabIndex = 4;
            this.currentContinentTab.Text = "Current Continent";
            this.currentContinentTab.UseVisualStyleBackColor = true;
            // 
            // mapTestContinent
            // 
            this.mapTestContinent.Location = new System.Drawing.Point(7, 7);
            this.mapTestContinent.Name = "mapTestContinent";
            this.mapTestContinent.Size = new System.Drawing.Size(800, 600);
            this.mapTestContinent.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1077, 711);
            this.Controls.Add(this.tabs);
            this.Name = "Form1";
            this.Text = "LoreGen Worldbuilder";
            this.climatesTab.ResumeLayout(false);
            this.climatesTab.PerformLayout();
            this.testContTab.ResumeLayout(false);
            this.testContTab.PerformLayout();
            this.langTestTab.ResumeLayout(false);
            this.testMapTab.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.currentContinentTab.ResumeLayout(false);
            this.ResumeLayout(false);

        }        

        #endregion

        private System.Windows.Forms.TabPage climatesTab;
        private System.Windows.Forms.RichTextBox climateTextBox;
        private System.Windows.Forms.TabPage testContTab;
        private System.Windows.Forms.RichTextBox testContInfoBox;
        private System.Windows.Forms.TabPage langTestTab;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TabPage testMapTab;
        private System.Windows.Forms.ListBox contextListBox;
        private System.Windows.Forms.Button newWorldButton;
        private System.Windows.Forms.Panel mapTest;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.RichTextBox regionsTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.RichTextBox biomeTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RichTextBox terrainTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.RichTextBox currentBlockInfo;
        private System.Windows.Forms.TabPage currentContinentTab;
        private System.Windows.Forms.Panel mapTestContinent;
        private System.Windows.Forms.Button mapBackButton;

    }
}

