import { Component, EventEmitter, Output } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-add-file',
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './add-file.html',
  styleUrl: './add-file.css'
})
export class AddFileComponent {
  @Output() fileUploaded = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  fileForm: FormGroup;
  fileToUpload: File | null = null;

  categoryOptions = [
    "Uncategorized", "Policies", "Invoice", "Reports", "Blueprint", "SafetyInstructions", "Notices", "Manuals",
    "MeetingMinutes", "Prescriptions", "Documentation", "ReleaseNotes", "TestCases", "DesignDocs", "Itinerary",
    "FlightTickets", "HotelBookings"
  ];

  allItems: any[] = [];
  itemNames: string[] = [];
  filteredCategories: string[] = [];

  constructor(private fb: FormBuilder, private adminService: AdminService) {
    this.fileForm = this.fb.group({
      file: [null, Validators.required],
      itemName: ['', Validators.required],
      itemCategory: ['', Validators.required],
      category: ['Uncategorized', Validators.required],
      description: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.adminService.getAllItems().subscribe({
      next: items => {
        this.allItems = items;
        this.itemNames = Array.from(new Set(items.map((i: any) => i.name)));
      },
      error: () => alert('Failed to load items.')
    });
  }

  onItemNameChange(): void {
    const name = this.fileForm.value.itemName;
    const matchingItems = this.allItems.filter(i => i.name === name);
    const allCategories = matchingItems.flatMap(i => i.categories);
    this.filteredCategories = Array.from(new Set(allCategories));
    this.fileForm.patchValue({ itemCategory: '' });
  }

  onFileChange(event: any): void {
    const file = event.target.files?.[0];
    if (file) {
      this.fileToUpload = file;
      this.fileForm.patchValue({ file });
    }
  }

  submit(): void {
    if (this.fileForm.invalid || !this.fileToUpload) {
      this.fileForm.markAllAsTouched();
      alert('Please complete all required fields.');
      return;
    }

    const formValues = this.fileForm.value;
    const matchedItem = this.allItems.find(
      i => i.name === formValues.itemName && i.categories.includes(formValues.itemCategory)
    );

    if (!matchedItem) {
      alert('No matching item found!');
      return;
    }

    const formData = new FormData();
    formData.append('file', this.fileToUpload);
    formData.append('itemId', matchedItem.id);
    formData.append('category', formValues.category);
    formData.append('description', formValues.description);

    this.adminService.uploadFile(formData).subscribe({
      next: () => {
        alert('File uploaded successfully!');
        this.fileUploaded.emit();
      },
      error: () => alert('Failed to upload file.')
    });
  }

  onCancel(): void {
    if (confirm('Are you sure you want to cancel the file upload?')) {
      this.cancel.emit();
    }
  }


}
