using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace ConfuserExSwitchKiller
{
	// Token: 0x020002CD RID: 717
	public class MainForm : Form
	{
		// Token: 0x0600214C RID: 8524 RVA: 0x0008FA29 File Offset: 0x0008EA29
		public MainForm()
		{
			InitializeComponent();
		}

		// Token: 0x06002151 RID: 8529 RVA: 0x0008FCB4 File Offset: 0x0008ECB4
		public void AddMethods(TypeDef type)
		{
			if (type.HasMethods)
                foreach (var current in type.Methods)
                    if (current.HasBody)
                        methods.Add(current);

            if (!type.HasNestedTypes) return;
            foreach (var current2 in type.NestedTypes) AddMethods(current2);
        }

		// Token: 0x0600214D RID: 8525 RVA: 0x0008FA60 File Offset: 0x0008EA60
		private void Button1Click(object sender, EventArgs e)
		{
			label2.Text = "";
			var openFileDialog = new OpenFileDialog();
			openFileDialog.Title = "Browse for target assembly";
			openFileDialog.InitialDirectory = "c:\\";
			if (DirectoryName != "") openFileDialog.InitialDirectory = DirectoryName;
            openFileDialog.Filter = "All files (*.exe,*.dll)|*.exe;*.dll";
			openFileDialog.FilterIndex = 2;
			openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() != DialogResult.OK) return;
            var fileName = openFileDialog.FileName;
            textBox1.Text = fileName;
            var num = fileName.LastIndexOf("\\", StringComparison.Ordinal);
            if (num != -1) DirectoryName = fileName.Remove(num, fileName.Length - num);
            if (DirectoryName.Length == 2) DirectoryName += "\\";
        }

        private void InitCode()
        {
            
        }

		// Token: 0x06002154 RID: 8532 RVA: 0x00090DF4 File Offset: 0x0008FDF4
		private void Button2Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBox1.Text)) return;
            var text = Path.GetDirectoryName(textBox1.Text);
            if (text != null && !text.EndsWith("\\")) text += "\\";
            var filename = text + Path.GetFileNameWithoutExtension(textBox1.Text) + "_deobfuscated" + Path.GetExtension(textBox1.Text);
            var assemblyDef = AssemblyDef.Load(textBox1.Text);
            var manifestModule = assemblyDef.ManifestModule;
            if (!manifestModule.IsILOnly) return;
            var moduleWriterOptions = new ModuleWriterOptions(manifestModule);
            moduleWriterOptions.MetaDataOptions.Flags |= (MetaDataFlags.PreserveTypeRefRids | MetaDataFlags.PreserveTypeDefRids | MetaDataFlags.PreserveFieldRids | MetaDataFlags.PreserveMethodRids | MetaDataFlags.PreserveParamRids | MetaDataFlags.PreserveMemberRefRids | MetaDataFlags.PreserveStandAloneSigRids | MetaDataFlags.PreserveEventRids | MetaDataFlags.PreservePropertyRids | MetaDataFlags.PreserveTypeSpecRids | MetaDataFlags.PreserveMethodSpecRids | MetaDataFlags.PreserveUSOffsets | MetaDataFlags.PreserveBlobOffsets | MetaDataFlags.PreserveExtraSignatureData | MetaDataFlags.KeepOldMaxStack);
            methods = new List<MethodDef>();
            if (manifestModule.HasTypes)
                foreach (var current in manifestModule.Types)
                    AddMethods(current);
            var blocksCflowDeobfuscator = new BlocksCflowDeobfuscator();
            for (var i = 0; i < methods.Count; i++)
            {
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
            }

            for (var i = 0; i < methods.Count; i++)
            {
                for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                {
                    if (methods[i].Body.Instructions[j].IsLdcI4() &&
                        j + 1 < methods[i].Body.Instructions.Count &&
                        methods[i].Body.Instructions[j + 1].OpCode == OpCodes.Pop)
                    {
                        methods[i].Body.Instructions[j].OpCode = OpCodes.Nop;
                        methods[i].Body.Instructions[j + 1].OpCode = OpCodes.Nop;
                        for (var k = 0; k < methods[i].Body.Instructions.Count; k++)
                        {
                            if (methods[i].Body.Instructions[k].OpCode != OpCodes.Br &&
                                methods[i].Body.Instructions[k].OpCode != OpCodes.Br_S) continue;
                            if (!(methods[i].Body.Instructions[k].Operand is Instruction)) continue;
                            var instruction = methods[i].Body.Instructions[k].Operand as Instruction;
                            if (instruction != methods[i].Body.Instructions[j + 1]) continue;
                            if (k - 1 >= 0 && methods[i].Body.Instructions[k - 1].IsLdcI4())
                                methods[i].Body.Instructions[k - 1].OpCode = OpCodes.Nop;
                        }
                    }

                    if (methods[i].Body.Instructions[j].OpCode != OpCodes.Dup ||
                        j + 1 >= methods[i].Body.Instructions.Count ||
                        methods[i].Body.Instructions[j + 1].OpCode != OpCodes.Pop) continue;
                    
                    methods[i].Body.Instructions[j].OpCode = OpCodes.Nop;
                    methods[i].Body.Instructions[j + 1].OpCode = OpCodes.Nop;
                    for (var k = 0; k < methods[i].Body.Instructions.Count; k++)
                    {
                        if (methods[i].Body.Instructions[k].OpCode != OpCodes.Br &&
                            methods[i].Body.Instructions[k].OpCode != OpCodes.Br_S) continue;

                        if (!(methods[i].Body.Instructions[k].Operand is Instruction)) continue;

                        var instruction = methods[i].Body.Instructions[k].Operand as Instruction;
                        if (instruction != methods[i].Body.Instructions[j + 1]) continue;
                        if (k - 1 >= 0 && methods[i].Body.Instructions[k - 1].OpCode == OpCodes.Dup)
                            methods[i].Body.Instructions[k - 1].OpCode = OpCodes.Nop;
                    }
                }
            }

            for (var i = 0; i < methods.Count; i++)
            {
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
            }

            for (var i = 0; i < methods.Count; i++)
            {
                var list2 = new List<Instruction>();
                var list3 = new List<Instruction>();
                Local local = null;
                var list4 = new List<int>();
                var list5 = new List<int>();
                for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                {
                    if (j + 3 < methods[i].Body.Instructions.Count && methods[i].Body.Instructions[j].IsLdcI4())
                    {
                        if (methods[i].Body.Instructions[j + 1].IsLdcI4())
                            //if (this.methods[i].Body.Instructions[j + 1].OpCode == OpCodes.Xor)
                        {
                            //if (this.methods[i].Body.Instructions[j + 2].OpCode == OpCodes.Dup)
                            if (methods[i].Body.Instructions[j + 2].OpCode == OpCodes.Xor)
                            {
                                //if (this.methods[i].Body.Instructions[j + 3].IsStloc())
                                {
                                    //if (this.methods[i].Body.Instructions[j + 4].IsLdcI4())
                                    {
                                        //if (this.methods[i].Body.Instructions[j + 5].OpCode == OpCodes.Rem_Un)
                                        {
                                            //if (this.methods[i].Body.Instructions[j + 6].OpCode == OpCodes.Switch)
                                            if (methods[i].Body.Instructions[j + 3].OpCode == OpCodes.Switch)
                                            {
                                                list2.Add(methods[i].Body.Instructions[j]);
                                                list4.Add(methods[i].Body.Instructions[j].GetLdcI4Value());
                                                //local = this.methods[i].Body.Instructions[j + 3].GetLocal(this.methods[i].Body.Variables);
                                                list5.Add(methods[i].Body.Instructions[j + 1].GetLdcI4Value());
                                                list3.Add(methods[i].Body.Instructions[j + 3]);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        /*
                                if (this.methods[i].Body.Instructions[j + 1].OpCode == OpCodes.Xor)
                                {
                                    if (this.methods[i].Body.Instructions[j + 2].OpCode == OpCodes.Dup)
                                    {
                                        if (this.methods[i].Body.Instructions[j + 3].IsStloc())
                                        {
                                            if (this.methods[i].Body.Instructions[j + 4].IsLdcI4())
                                            {
                                                if (this.methods[i].Body.Instructions[j + 5].OpCode == OpCodes.Rem_Un)
                                                {
                                                    if (this.methods[i].Body.Instructions[j + 6].OpCode == OpCodes.Switch)
                                                    {
                                                        list2.Add(this.methods[i].Body.Instructions[j]);
                                                        list4.Add(this.methods[i].Body.Instructions[j].GetLdcI4Value());
                                                        local = this.methods[i].Body.Instructions[j + 3].GetLocal(this.methods[i].Body.Variables);
                                                        list5.Add(this.methods[i].Body.Instructions[j + 4].GetLdcI4Value());
                                                        list3.Add(this.methods[i].Body.Instructions[j + 6]);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                */

                    }
                }
                if (list2.Count > 0)
                {
                    for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                    {
                        if (j + 1 < methods[i].Body.Instructions.Count && methods[i].Body.Instructions[j].IsLdcI4())
                        {
                            if (methods[i].Body.Instructions[j + 1].IsBr())
                            {
                                var instruction = methods[i].Body.Instructions[j + 1].Operand as Instruction;
                                for (var k = 0; k < list2.Count; k++)
                                {
                                    if (instruction == list2[k])
                                    {
                                        var methodDef = methods[i];
                                        var ldcI4Value = methods[i].Body.Instructions[j].GetLdcI4Value();
                                        var num = (uint)(ldcI4Value ^ list4[k]);
                                        var num2 = num % (uint)list5[k];
                                        methods[i].Body.Instructions[j].OpCode = OpCodes.Ldc_I4;
                                        methods[i].Body.Instructions[j].Operand = (int)num;
                                        methods[i].Body.Instructions[j + 1].Operand = OpCodes.Br;
                                        var array = list3[k].Operand as Instruction[];
                                        methods[i].Body.Instructions[j + 1].Operand = array[(int)((UIntPtr)num2)];
                                        //this.methods[i].Body.Instructions.Insert(j + 1, OpCodes.Stloc_S.ToInstruction(local));
                                        j++;
                                    }
                                }
                            }
                        }
                    }
                    methods[i].Body.SimplifyBranches();
                    methods[i].Body.OptimizeBranches();
                }
            }
            for (var i = 0; i < methods.Count; i++)
            {
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
            }
            for (var i = 0; i < methods.Count; i++)
            {
                var dictionary = new Dictionary<Instruction, Instruction>();
                for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                {
                    if (methods[i].Body.Instructions[j].IsConditionalBranch())
                    {
                        var instruction2 = methods[i].Body.Instructions[j];
                        for (var k = 0; k < methods[i].Body.Instructions.Count; k++)
                        {
                            if (methods[i].Body.Instructions[k].IsBr())
                            {
                                var instruction3 = methods[i].Body.Instructions[k];
                                var instruction4 = methods[i].Body.Instructions[k].Operand as Instruction;
                                if (instruction4 == instruction2)
                                {
                                    if (!dictionary.ContainsKey(instruction4))
                                    {
                                        methods[i].Body.Instructions[k].OpCode = instruction2.GetOpCode();
                                        methods[i].Body.Instructions[k].Operand = instruction2.GetOperand();
                                        methods[i].Body.Instructions.Insert(k + 1, OpCodes.Br.ToInstruction(methods[i].Body.Instructions[j + 1]));
                                        k++;
                                        dictionary.Add(instruction4, methods[i].Body.Instructions[k]);
                                    }
                                }
                            }
                        }
                    }
                }
                methods[i].Body.SimplifyBranches();
                methods[i].Body.OptimizeBranches();
            }
            for (var i = 0; i < methods.Count; i++)
            {
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
            }
            var num3 = 0;
            for (var i = 0; i < methods.Count; i++)
            {
                toberemoved = new List<int>();
                integer_values_1 = new List<int>();
                for_rem = new List<int>();
                switchinstructions = new List<Instruction>();
                for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                {
                    if (j + 6 < methods[i].Body.Instructions.Count && methods[i].Body.Instructions[j].IsLdcI4())
                    {
                        if (methods[i].Body.Instructions[j + 1].OpCode == OpCodes.Xor)
                        {
                            //if (this.methods[i].Body.Instructions[j + 2].OpCode == OpCodes.Dup)
                            {
                                //if (this.methods[i].Body.Instructions[j + 3].IsStloc())
                                {
                                    //if (this.methods[i].Body.Instructions[j + 4].IsLdcI4())
                                    {
                                        //if (this.methods[i].Body.Instructions[j + 5].OpCode == OpCodes.Rem_Un)
                                        {
                                            if (methods[i].Body.Instructions[j + 6].OpCode == OpCodes.Switch)
                                            {
                                                toberemoved.Add(j);
                                                integer_values_1.Add(methods[i].Body.Instructions[j].GetLdcI4Value());
                                                local_variable = methods[i].Body.Instructions[j + 3].GetLocal(methods[i].Body.Variables);
                                                for_rem.Add(methods[i].Body.Instructions[j + 4].GetLdcI4Value());
                                                switchinstructions.Add(methods[i].Body.Instructions[j + 6]);

                                                //list2.Add(this.methods[i].Body.Instructions[j]);
                                                //list4.Add(this.methods[i].Body.Instructions[j].GetLdcI4Value());
                                                ////local = this.methods[i].Body.Instructions[j + 3].GetLocal(this.methods[i].Body.Variables);
                                                //list5.Add(this.methods[i].Body.Instructions[j + 1].GetLdcI4Value());
                                                //list3.Add(this.methods[i].Body.Instructions[j + 3]);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (switchinstructions.Count > 0)
                {
                    toberemovedindex = new List<int>();
                    toberemovedvalues = new List<int>();
                    conditionalinstructions = new List<Instruction>();
                    brinstructions = new List<Instruction>();
                    realbrinstructions = new List<Instruction>();
                    local_values = new List<int>();
                    instructions = methods[i].Body.Instructions;
                    method = methods[i];
                    InstructionParse2(0, 0u);
                    num3 += toberemovedindex.Count;
                    if (toberemovedindex.Count > 0)
                    {
                        for (var l = 0; l < toberemoved.Count; l++)
                        {
                            for (var j = 0; j < 6; j++)
                            {
                                methods[i].Body.Instructions[j + toberemoved[l]].OpCode = OpCodes.Nop;
                                methods[i].Body.Instructions[j + toberemoved[l]].Operand = null;
                            }
                        }
                        for (var j = 0; j < toberemovedindex.Count; j++)
                        {
                            methods[i].Body.Instructions[toberemovedindex[j]].OpCode = OpCodes.Ldc_I4;
                            methods[i].Body.Instructions[toberemovedindex[j]].Operand = toberemovedvalues[j];
                            if (!methods[i].Body.Instructions[toberemovedindex[j] + 1].IsBr())
                            {
                                for (var k = 0; k < 4; k++)
                                {
                                    methods[i].Body.Instructions[toberemovedindex[j] + k + 1].OpCode = OpCodes.Nop;
                                    methods[i].Body.Instructions[toberemovedindex[j] + k + 1].Operand = null;
                                }
                            }
                        }
                    }
                }
                toberemoved = new List<int>();
                integer_values_1 = new List<int>();
                for_rem = new List<int>();
                switchinstructions = new List<Instruction>();
                for (var j = 0; j < methods[i].Body.Instructions.Count; j++)
                {
                    if (j + 6 < methods[i].Body.Instructions.Count && methods[i].Body.Instructions[j].IsLdcI4())
                    {
                        if (methods[i].Body.Instructions[j + 1].OpCode == OpCodes.Xor)
                        {
                            if (methods[i].Body.Instructions[j + 2].IsLdcI4())
                            {
                                //if (this.methods[i].Body.Instructions[j + 3].OpCode == OpCodes.Rem_Un)
                                {
                                    if (methods[i].Body.Instructions[j + 4].OpCode == OpCodes.Switch)
                                    {
                                        toberemoved.Add(j);
                                        integer_values_1.Add(methods[i].Body.Instructions[j].GetLdcI4Value());
                                        for_rem.Add(methods[i].Body.Instructions[j + 2].GetLdcI4Value());
                                        switchinstructions.Add(methods[i].Body.Instructions[j + 4]);
                                    }
                                }
                            }
                        }
                    }
                }
                if (switchinstructions.Count > 0)
                {
                    toberemovedindex = new List<int>();
                    toberemovedvalues = new List<int>();
                    conditionalinstructions = new List<Instruction>();
                    brinstructions = new List<Instruction>();
                    realbrinstructions = new List<Instruction>();
                    instructions = methods[i].Body.Instructions;
                    method = methods[i];
                    InstructionParseNoLocal(0);
                    num3 += toberemovedindex.Count;
                    if (toberemovedindex.Count > 0)
                    {
                        for (var l = 0; l < toberemoved.Count; l++)
                        {
                            for (var j = 0; j < 4; j++)
                            {
                                methods[i].Body.Instructions[j + toberemoved[l]].OpCode = OpCodes.Nop;
                                methods[i].Body.Instructions[j + toberemoved[l]].Operand = null;
                            }
                        }
                        for (var j = 0; j < toberemovedindex.Count; j++)
                        {
                            methods[i].Body.Instructions[toberemovedindex[j]].OpCode = OpCodes.Ldc_I4;
                            methods[i].Body.Instructions[toberemovedindex[j]].Operand = toberemovedvalues[j];
                            if (!methods[i].Body.Instructions[toberemovedindex[j] + 1].IsBr())
                            {
                                for (var k = 0; k < 4; k++)
                                {
                                    methods[i].Body.Instructions[toberemovedindex[j] + k + 1].OpCode = OpCodes.Nop;
                                    methods[i].Body.Instructions[toberemovedindex[j] + k + 1].Operand = null;
                                }
                            }
                        }
                    }
                }
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
                methods[i].Body.SimplifyBranches();
                methods[i].Body.OptimizeBranches();
            }
            for (var i = 0; i < methods.Count; i++)
            {
                var blocks = new Blocks(methods[i]);
                blocksCflowDeobfuscator.Initialize(blocks);
                blocksCflowDeobfuscator.Deobfuscate();
                blocks.RepartitionBlocks();
                blocks.GetCode(out var list, out var exceptionHandlers);
                DotNetUtils.RestoreBody(methods[i], list, exceptionHandlers);
            }
            moduleWriterOptions.Logger = DummyLogger.NoThrowInstance;
            manifestModule.Write(filename, moduleWriterOptions);
            label2.Text = "File deobfuscated! " + num3.ToString() + " replaces made!";
        }

		// Token: 0x06002150 RID: 8528 RVA: 0x0008FCAB File Offset: 0x0008ECAB
		private void Button3Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		// Token: 0x06002155 RID: 8533 RVA: 0x00092A98 File Offset: 0x00091A98
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		// Token: 0x06002156 RID: 8534 RVA: 0x00092AD4 File Offset: 0x00091AD4
		private void InitializeComponent()
		{
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button4 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(786, 185);
            this.button3.Margin = new System.Windows.Forms.Padding(6);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(144, 50);
            this.button3.TabIndex = 29;
            this.button3.Text = "Exit";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.Button3Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(350, 185);
            this.button2.Margin = new System.Windows.Forms.Padding(6);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(180, 50);
            this.button2.TabIndex = 28;
            this.button2.Text = "Deobfuscate";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.Button2Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(92, 185);
            this.button1.Margin = new System.Windows.Forms.Padding(6);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(246, 50);
            this.button1.TabIndex = 27;
            this.button1.Text = "Browse for assembly";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.Button1Click);
            // 
            // label2
            // 
            this.label2.ForeColor = System.Drawing.Color.Blue;
            this.label2.Location = new System.Drawing.Point(224, 137);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(586, 42);
            this.label2.TabIndex = 26;
            this.label2.Text = "Current status";
            // 
            // label1
            // 
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.ForeColor = System.Drawing.Color.Black;
            this.label1.Location = new System.Drawing.Point(92, 60);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(200, 27);
            this.label1.TabIndex = 25;
            this.label1.Text = "Name of assembly:";
            // 
            // textBox1
            // 
            this.textBox1.AllowDrop = true;
            this.textBox1.Location = new System.Drawing.Point(92, 92);
            this.textBox1.Margin = new System.Windows.Forms.Padding(6);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(834, 31);
            this.textBox1.TabIndex = 24;
            this.textBox1.Text = "D:\\wWw\\.dev\\MiChangerA1\\Services.dll";
            this.textBox1.DragDrop += new System.Windows.Forms.DragEventHandler(this.TextBox1DragDrop);
            this.textBox1.DragEnter += new System.Windows.Forms.DragEventHandler(this.TextBox1DragEnter);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(542, 185);
            this.button4.Margin = new System.Windows.Forms.Padding(6);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(180, 50);
            this.button4.TabIndex = 30;
            this.button4.Text = "Deobfuscate1";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1022, 292);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox1);
            this.Margin = new System.Windows.Forms.Padding(6);
            this.Name = "MainForm";
            this.Text = "ConfuserEx 5.0 Switch Killer 1.0 by CodeCracker";
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		// Token: 0x06002152 RID: 8530 RVA: 0x0008FD90 File Offset: 0x0008ED90
		public void InstructionParse2(int ins_index, uint local_value)
		{
			for (var i = ins_index; i < instructions.Count; i++)
			{
				var instruction = instructions[i];
				var methodDef = method;
				string text = methodDef.Name;
				var fullName = methodDef.DeclaringType.FullName;
				if (!toberemovedindex.Contains(i))
				{
					if (instructions[i].IsBr())
					{
						var item = instructions[i].Operand as Instruction;
						if (!brinstructions.Contains(item) && !realbrinstructions.Contains(item))
						{
							realbrinstructions.Add(item);
							var ins_index2 = instructions.IndexOf(item);
							InstructionParse2(ins_index2, local_value);
						}
						break;
					}
					if (instructions[i].IsConditionalBranch() || instructions[i].IsLeave())
					{
						var item = instructions[i].Operand as Instruction;
						if (!conditionalinstructions.Contains(item))
						{
							conditionalinstructions.Add(item);
							var ins_index3 = instructions.IndexOf(item);
							InstructionParse2(ins_index3, local_value);
							if (i + 1 < instructions.Count)
							{
								var ins_index4 = i + 1;
								InstructionParse2(ins_index4, local_value);
							}
						}
					}
					else
					{
						if (instructions[i].OpCode == OpCodes.Ret)
						{
							break;
						}
						if (instructions[i].IsLdcI4() && i + 1 < instructions.Count && instructions[i + 1].IsStloc() && instructions[i + 1].GetLocal(method.Body.Variables) == local_variable)
						{
							local_value = (uint)instructions[i].GetLdcI4Value();
						}
						else if (instructions[i].IsLdcI4() || (instructions[i].IsLdloc() && instructions[i].GetLocal(method.Body.Variables) == local_variable))
						{
							uint num;
							if (instructions[i].IsLdcI4())
							{
								num = (uint)instructions[i].GetLdcI4Value();
							}
							else
							{
								num = local_value;
							}
							var num2 = i + 1;
							if (instructions[i + 1].IsBr())
							{
								var item2 = instructions[i + 1].Operand as Instruction;
								num2 = instructions.IndexOf(item2);
							}
							if (instructions[num2].IsLdcI4() || (instructions[num2].IsLdloc() && instructions[num2].GetLocal(method.Body.Variables) == local_variable))
							{
								uint num3;
								if (instructions[num2].IsLdcI4())
								{
									num3 = (uint)instructions[num2].GetLdcI4Value();
								}
								else
								{
									num3 = local_value;
								}
								var num4 = 0u;
								if ((instructions[num2 + 1].OpCode == OpCodes.Mul && instructions[num2 + 2].IsLdcI4()) || (instructions[num2 + 1].IsLdcI4() && instructions[num2 + 2].OpCode == OpCodes.Mul) || instructions[num2 + 1].OpCode == OpCodes.Xor)
								{
									if (instructions[num2 + 1].OpCode != OpCodes.Xor)
									{
										if (instructions[num2 + 1].OpCode == OpCodes.Mul && instructions[num2 + 2].IsLdcI4())
										{
											num4 = (uint)instructions[num2 + 2].GetLdcI4Value();
										}
										if (instructions[num2 + 1].IsLdcI4() && instructions[num2 + 2].OpCode == OpCodes.Mul)
										{
											num4 = (uint)instructions[num2 + 1].GetLdcI4Value();
										}
									}
									if (instructions[num2 + 3].OpCode == OpCodes.Xor || instructions[num2 + 1].OpCode == OpCodes.Xor)
									{
										for (var j = 0; j < toberemoved.Count; j++)
										{
											if ((instructions[num2 + 4].IsBr() && instructions[num2 + 4].Operand as Instruction == instructions[toberemoved[j]]) || num2 + 4 == toberemoved[j] || (instructions[num2 + 1].OpCode == OpCodes.Xor && num2 == toberemoved[j]))
											{
												uint num5;
												if (instructions[num2 + 1].OpCode == OpCodes.Xor)
												{
													num5 = (num ^ num3);
												}
												else if (instructions[num2 + 1].IsLdcI4() || instructions[num2 + 1].IsLdloc())
												{
													num5 = (num4 * num3 ^ num);
												}
												else
												{
													num5 = (num * num3 ^ num4);
												}
												if (instructions[num2 + 1].OpCode != OpCodes.Xor)
												{
													local_value = (num5 ^ (uint)integer_values_1[j]);
												}
												else
												{
													local_value = num5;
												}
												var num6 = local_value % (uint)for_rem[j];
												var array = switchinstructions[j].Operand as Instruction[];
												var item3 = array[(int)((UIntPtr)num6)];
												if (toberemovedindex.Contains(i))
												{
												}
												toberemovedindex.Add(i);
												toberemovedvalues.Add((int)num6);
												var flag = false;
												var num7 = brinstructions.IndexOf(item3);
												if (num7 != -1)
												{
													var num8 = local_values[num7];
													if ((long)num8 != (long)((ulong)local_value))
													{
														flag = true;
													}
												}
												else
												{
													flag = true;
												}
												if (flag)
												{
													brinstructions.Add(item3);
													local_values.Add((int)local_value);
													InstructionParse2(instructions.IndexOf(item3), local_value);
													break;
												}
											}
										}
									}
								}
							}
						}
						else if (instructions[i].OpCode == OpCodes.Switch)
						{
							bool flag2;
							if (i - 4 < 0)
							{
								flag2 = false;
							}
							else
							{
								flag2 = false;
								for (var j = 0; j < toberemoved.Count; j++)
								{
									var num9 = toberemoved[j];
									if (i - 6 == toberemoved[j])
									{
										flag2 = true;
										break;
									}
								}
							}
							if (!flag2)
							{
								var array2 = instructions[i].Operand as Instruction[];
								for (var j = 0; j < array2.Length; j++)
								{
									var item4 = array2[j];
									InstructionParse2(instructions.IndexOf(item4), local_value);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06002153 RID: 8531 RVA: 0x0009067C File Offset: 0x0008F67C
		public void InstructionParseNoLocal(int ins_index)
		{
			for (var i = ins_index; i < instructions.Count; i++)
			{
				var instruction = instructions[i];
				var methodDef = method;
				if (!toberemovedindex.Contains(i))
				{
					if (instructions[i].IsBr())
					{
						var item = instructions[i].Operand as Instruction;
						if (!brinstructions.Contains(item) && !realbrinstructions.Contains(item))
						{
							realbrinstructions.Add(item);
							var ins_index2 = instructions.IndexOf(item);
							InstructionParseNoLocal(ins_index2);
						}
						break;
					}
					if (instructions[i].IsConditionalBranch() || instructions[i].IsLeave())
					{
						var item = instructions[i].Operand as Instruction;
						if (!conditionalinstructions.Contains(item))
						{
							conditionalinstructions.Add(item);
							var ins_index3 = instructions.IndexOf(item);
							InstructionParseNoLocal(ins_index3);
							if (i + 1 < instructions.Count)
							{
								var ins_index4 = i + 1;
								InstructionParseNoLocal(ins_index4);
							}
						}
					}
					else
					{
						if (instructions[i].OpCode == OpCodes.Ret)
						{
							break;
						}
						if (instructions[i].IsLdcI4())
						{
							var num = 0u;
							if (instructions[i].IsLdcI4())
							{
								num = (uint)instructions[i].GetLdcI4Value();
							}
							var num2 = i + 1;
							if (instructions[i + 1].IsBr())
							{
								var item2 = instructions[i + 1].Operand as Instruction;
								num2 = instructions.IndexOf(item2);
							}
							if (instructions[num2].IsLdcI4())
							{
								var num3 = 0u;
								if (instructions[num2].IsLdcI4())
								{
									num3 = (uint)instructions[num2].GetLdcI4Value();
								}
								var num4 = 0u;
								if ((instructions[num2 + 1].OpCode == OpCodes.Mul && instructions[num2 + 2].IsLdcI4()) || (instructions[num2 + 1].IsLdcI4() && instructions[num2 + 2].OpCode == OpCodes.Mul) || instructions[num2 + 1].OpCode == OpCodes.Xor)
								{
									if (instructions[num2 + 1].OpCode != OpCodes.Xor)
									{
										if (instructions[num2 + 1].OpCode == OpCodes.Mul && instructions[num2 + 2].IsLdcI4())
										{
											num4 = (uint)instructions[num2 + 2].GetLdcI4Value();
										}
										if (instructions[num2 + 1].IsLdcI4() && instructions[num2 + 2].OpCode == OpCodes.Mul)
										{
											num4 = (uint)instructions[num2 + 1].GetLdcI4Value();
										}
									}
									if (instructions[num2 + 3].OpCode == OpCodes.Xor || instructions[num2 + 1].OpCode == OpCodes.Xor)
									{
										for (var j = 0; j < toberemoved.Count; j++)
										{
											if ((instructions[num2 + 4].IsBr() && instructions[num2 + 4].Operand as Instruction == instructions[toberemoved[j]]) || num2 + 4 == toberemoved[j] || (instructions[num2 + 1].OpCode == OpCodes.Xor && num2 == toberemoved[j]))
											{
												uint num5;
												if (instructions[num2 + 1].OpCode == OpCodes.Xor)
												{
													num5 = (num ^ num3);
												}
												else if (instructions[num2 + 1].IsLdcI4() || instructions[num2 + 1].IsLdloc())
												{
													num5 = (num4 * num3 ^ num);
												}
												else
												{
													num5 = (num * num3 ^ num4);
												}
												uint num6;
												if (instructions[num2 + 1].OpCode != OpCodes.Xor)
												{
													num6 = (num5 ^ (uint)integer_values_1[j]);
												}
												else
												{
													num6 = num5;
												}
												var num7 = num6 % (uint)for_rem[j];
												var array = switchinstructions[j].Operand as Instruction[];
												var item3 = array[(int)((UIntPtr)num7)];
												if (toberemovedindex.Contains(i))
												{
												}
												toberemovedindex.Add(i);
												toberemovedvalues.Add((int)num7);
												var flag = false;
												if (brinstructions.IndexOf(item3) != -1)
												{
													flag = true;
												}
												if (flag)
												{
													brinstructions.Add(item3);
													InstructionParseNoLocal(instructions.IndexOf(item3));
													break;
												}
											}
										}
									}
								}
							}
						}
						else if (instructions[i].OpCode == OpCodes.Switch)
						{
							bool flag2;
							if (i - 4 < 0)
							{
								flag2 = false;
							}
							else
							{
								flag2 = false;
								for (var j = 0; j < toberemoved.Count; j++)
								{
									var num8 = toberemoved[j];
									if (i - 4 == toberemoved[j])
									{
										flag2 = true;
										break;
									}
								}
							}
							if (!flag2)
							{
								var array2 = instructions[i].Operand as Instruction[];
								for (var j = 0; j < array2.Length; j++)
								{
									var item4 = array2[j];
									InstructionParseNoLocal(instructions.IndexOf(item4));
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x0600214E RID: 8526 RVA: 0x0008FB58 File Offset: 0x0008EB58
		private void TextBox1DragDrop(object sender, DragEventArgs e)
		{
			try
			{
				var array = (Array)e.Data.GetData(DataFormats.FileDrop);
				if (array != null)
				{
					var text = array.GetValue(0).ToString();
					var num = text.LastIndexOf(".");
					if (num != -1)
					{
						var text2 = text.Substring(num);
						text2 = text2.ToLower();
						if (text2 == ".exe" || text2 == ".dll")
						{
							Activate();
							textBox1.Text = text;
							var num2 = text.LastIndexOf("\\");
							if (num2 != -1)
							{
								DirectoryName = text.Remove(num2, text.Length - num2);
							}
							if (DirectoryName.Length == 2)
							{
								DirectoryName += "\\";
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x0600214F RID: 8527 RVA: 0x0008FC74 File Offset: 0x0008EC74
		private void TextBox1DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

		// Token: 0x04000E84 RID: 3716
		private List<Instruction> brinstructions;

		// Token: 0x04000E8B RID: 3723
		private Button button1;

		// Token: 0x04000E8C RID: 3724
		private Button button2;

		// Token: 0x04000E8D RID: 3725
		private Button button3;

		// Token: 0x04000E87 RID: 3719
		private IContainer components = null;

		// Token: 0x04000E83 RID: 3715
		private List<Instruction> conditionalinstructions;

		// Token: 0x04000E78 RID: 3704
		public string DirectoryName = "";

		// Token: 0x04000E7D RID: 3709
		private List<int> for_rem;

		// Token: 0x04000E7F RID: 3711
		private IList<Instruction> instructions;

		// Token: 0x04000E7C RID: 3708
		private List<int> integer_values_1;

		// Token: 0x04000E89 RID: 3721
		private Label label1;

		// Token: 0x04000E8A RID: 3722
		private Label label2;

		// Token: 0x04000E86 RID: 3718
		private List<int> local_values;

		// Token: 0x04000E7A RID: 3706
		private Local local_variable = null;

		// Token: 0x04000E82 RID: 3714
		private MethodDef method;

		// Token: 0x04000E79 RID: 3705
		private List<MethodDef> methods = new List<MethodDef>();

		// Token: 0x04000E85 RID: 3717
		private List<Instruction> realbrinstructions;

		// Token: 0x04000E7E RID: 3710
		private List<Instruction> switchinstructions;

		// Token: 0x04000E88 RID: 3720
		private TextBox textBox1;

		// Token: 0x04000E7B RID: 3707
		private List<int> toberemoved;

		// Token: 0x04000E80 RID: 3712
		private List<int> toberemovedindex;
        private Button button4;

        // Token: 0x04000E81 RID: 3713
        private List<int> toberemovedvalues;

        private void button4_Click(object sender, EventArgs e)
        {
            if (!File.Exists(textBox1.Text)) return;
            var text = Path.GetDirectoryName(textBox1.Text);
            if (text != null && !text.EndsWith("\\")) text += "\\";
            var filename = text + Path.GetFileNameWithoutExtension(textBox1.Text) + "_deobfuscated" + Path.GetExtension(textBox1.Text);
            var assemblyDef = AssemblyDef.Load(textBox1.Text);
            var manifestModule = assemblyDef.ManifestModule;
            if (!manifestModule.IsILOnly) return;
            var moduleWriterOptions = new ModuleWriterOptions(manifestModule);
            moduleWriterOptions.MetaDataOptions.Flags |= (MetaDataFlags.PreserveTypeRefRids | MetaDataFlags.PreserveTypeDefRids | MetaDataFlags.PreserveFieldRids | MetaDataFlags.PreserveMethodRids | MetaDataFlags.PreserveParamRids | MetaDataFlags.PreserveMemberRefRids | MetaDataFlags.PreserveStandAloneSigRids | MetaDataFlags.PreserveEventRids | MetaDataFlags.PreservePropertyRids | MetaDataFlags.PreserveTypeSpecRids | MetaDataFlags.PreserveMethodSpecRids | MetaDataFlags.PreserveUSOffsets | MetaDataFlags.PreserveBlobOffsets | MetaDataFlags.PreserveExtraSignatureData | MetaDataFlags.KeepOldMaxStack);
            methods = new List<MethodDef>();
            if (manifestModule.HasTypes)
                foreach (var current in manifestModule.Types)
                    AddMethods(current);

            var method = methods.First(m => m.Body.Instructions.Any(i => i.IsLdcI4() && i.GetLdcI4Value() == 348848128));
            var methodBlock = new Blocks(method);
            var methodBlocks = methodBlock.MethodBlocks.GetAllBlocks();
            var swBlock  =  methodBlocks.First(block => block.Instructions.Last().OpCode == OpCodes.Switch);

            var switchFallThroughs = methodBlocks.FindAll(b => b.FallThrough == swBlock); // blocks that fallthrough to the switch block
            InstructionEmulator _instructionEmulator = new InstructionEmulator();
            _instructionEmulator.Initialize(methodBlock, true); //TODO: Remove temporary precaution

            var targets = swBlock.Targets;
            _instructionEmulator.Emulate(swBlock.Instructions, 0, swBlock.Instructions.Count-1);
            var val = _instructionEmulator.Peek();
        }
    }
}