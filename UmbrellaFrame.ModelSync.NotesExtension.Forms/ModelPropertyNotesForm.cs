using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UmbrellaFrame.ModelSync.NotesExtension.Models;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtension.Forms
{
    public sealed class ModelPropertyNotesForm : Form
    {
        private readonly ModelNotesService _notesService;
        private readonly string _modelName;
        private readonly string _propertyName;
        private readonly string _noteKey;
        private readonly string _displayTitle;
        private readonly DataGridView _notesGrid;
        private readonly TextBox _noteTextBox;
        private readonly Button _addButton;
        private readonly Button _editButton;
        private readonly Button _deleteButton;
        private readonly Label _titleLabel;

        public ModelPropertyNotesForm(ModelNotesService notesService, Type modelType, string propertyName)
            : this(notesService, modelType == null ? null : modelType.Name, propertyName)
        {
        }

        public ModelPropertyNotesForm(ModelNotesService notesService, string? modelName, string propertyName)
            : this(
                notesService,
                ModelNotesService.CreatePropertyKey(modelName ?? string.Empty, propertyName),
                $"{modelName}.{propertyName}",
                modelName,
                propertyName)
        {
        }

        public static ModelPropertyNotesForm ForNoteKey(
            ModelNotesService notesService,
            string noteKey,
            string displayTitle)
        {
            return new ModelPropertyNotesForm(notesService, noteKey, displayTitle, null, null);
        }

        private ModelPropertyNotesForm(
            ModelNotesService notesService,
            string noteKey,
            string displayTitle,
            string? modelName = null,
            string? propertyName = null)
        {
            _notesService = notesService ?? throw new ArgumentNullException(nameof(notesService));
            if (string.IsNullOrWhiteSpace(noteKey))
            {
                throw new ArgumentException("Note key cannot be empty.", nameof(noteKey));
            }

            _noteKey = noteKey;
            _displayTitle = string.IsNullOrWhiteSpace(displayTitle) ? noteKey : displayTitle;
            _modelName = modelName ?? string.Empty;
            _propertyName = propertyName ?? string.Empty;

            Text = "Notes";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 430);
            MinimumSize = new Size(520, 390);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _titleLabel = new Label
            {
                AutoSize = false,
                Text = _displayTitle,
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(16, 14),
                Size = new Size(520, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _notesGrid = new DataGridView
            {
                Location = new Point(16, 48),
                Size = new Size(528, 190),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeight = 28,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            ConfigureNotesGrid();
            _notesGrid.SelectionChanged += (_, _) => UpdateSelectedNoteState();
            _notesGrid.CellDoubleClick += (_, _) => CopySelectedNoteTextToEditor();

            _noteTextBox = new TextBox
            {
                Location = new Point(16, 252),
                Size = new Size(528, 92),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _addButton = new Button
            {
                Text = "Add",
                Location = new Point(292, 364),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _addButton.Click += (_, _) => AddNote();

            _editButton = new Button
            {
                Text = "Edit",
                Location = new Point(378, 364),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _editButton.Click += (_, _) => EditSelectedNote();

            _deleteButton = new Button
            {
                Text = "Delete",
                Location = new Point(464, 364),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _deleteButton.Click += (_, _) => DeleteSelectedNote();

            Controls.Add(_titleLabel);
            Controls.Add(_notesGrid);
            Controls.Add(_noteTextBox);
            Controls.Add(_addButton);
            Controls.Add(_editButton);
            Controls.Add(_deleteButton);

            Load += (_, _) => RefreshNotes();
        }

        private void ConfigureNotesGrid()
        {
            _notesGrid.EnableHeadersVisualStyles = false;
            _notesGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            _notesGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(36, 45, 58);
            _notesGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            _notesGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 239, 255);
            _notesGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(24, 32, 44);
            _notesGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _notesGrid.RowTemplate.Height = 64;
            _notesGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;

            _notesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Owner",
                HeaderText = "Author",
                DataPropertyName = nameof(NoteGridRow.Owner),
                Width = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            _notesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Date",
                HeaderText = "Date",
                DataPropertyName = nameof(NoteGridRow.Date),
                Width = 92,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            _notesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Text",
                HeaderText = "Description",
                DataPropertyName = nameof(NoteGridRow.Text),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void AddNote()
        {
            if (string.IsNullOrWhiteSpace(_noteTextBox.Text))
            {
                MessageBox.Show(this, "Note text cannot be empty.", "Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _notesService.AddNote(_noteKey, _noteTextBox.Text);
            _noteTextBox.Clear();
            RefreshNotes();
        }

        private void EditSelectedNote()
        {
            var note = GetSelectedNote();
            if (note == null)
            {
                return;
            }

            if (!_notesService.CanModify(note))
            {
                MessageBox.Show(this, "Only the note owner can edit this note.", "Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_noteTextBox.Text))
            {
                MessageBox.Show(this, "Note text cannot be empty.", "Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _notesService.UpdateNote(_noteKey, note.Id, _noteTextBox.Text);
            _noteTextBox.Clear();
            RefreshNotes();
        }

        private void DeleteSelectedNote()
        {
            var note = GetSelectedNote();
            if (note == null)
            {
                return;
            }

            if (!_notesService.CanModify(note))
            {
                MessageBox.Show(this, "Only the note owner can delete this note.", "Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(this, "Delete selected note?", "Notes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            _notesService.DeleteNote(_noteKey, note.Id);
            _noteTextBox.Clear();
            RefreshNotes();
        }

        private void CopySelectedNoteTextToEditor()
        {
            var note = GetSelectedNote();
            if (note != null)
            {
                _noteTextBox.Text = note.Text;
            }
        }

        private void RefreshNotes()
        {
            var selectedRow = GetSelectedRow();
            var selectedId = selectedRow == null ? null : selectedRow.Note.Id;
            var rows = _notesService
                .GetNotes(_noteKey)
                .Select(note => new NoteGridRow(note))
                .ToArray();

            _notesGrid.DataSource = rows;

            if (!string.IsNullOrEmpty(selectedId))
            {
                foreach (DataGridViewRow row in _notesGrid.Rows)
                {
                    if (row.DataBoundItem is NoteGridRow noteRow && noteRow.Note.Id == selectedId)
                    {
                        row.Selected = true;
                        break;
                    }
                }
            }

            UpdateSelectedNoteState();
        }

        private void UpdateSelectedNoteState()
        {
            var note = GetSelectedNote();
            var canModify = note != null && _notesService.CanModify(note);

            _editButton.Enabled = canModify;
            _deleteButton.Enabled = canModify;
        }

        private NoteGridRow? GetSelectedRow()
        {
            if (_notesGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            return _notesGrid.SelectedRows[0].DataBoundItem as NoteGridRow;
        }

        private ModelNoteEntry? GetSelectedNote()
        {
            return GetSelectedRow()?.Note;
        }

        private sealed class NoteGridRow
        {
            public NoteGridRow(ModelNoteEntry note)
            {
                Note = note;
                Owner = string.IsNullOrWhiteSpace(note.CreatedBy.Name)
                    ? note.CreatedBy.Id
                    : note.CreatedBy.Name;
                Date = note.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy");
                Text = note.Text;
            }

            public ModelNoteEntry Note { get; }

            public string Owner { get; }

            public string Date { get; }

            public string Text { get; }
        }
    }
}
