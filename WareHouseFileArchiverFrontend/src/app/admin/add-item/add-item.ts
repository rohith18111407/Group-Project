import { Component, EventEmitter, inject, Output } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-add-item',
  imports: [FormsModule, ReactiveFormsModule],
  templateUrl: './add-item.html',
  styleUrl: './add-item.css'
})
export class AddItemComponent {
  @Output() cancel = new EventEmitter<void>();
  @Output() itemCreated = new EventEmitter<void>();

  adminService = inject(AdminService);

  itemForm = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.minLength(3)]),
    description: new FormControl('', [Validators.required]),
    categories: new FormControl('', [Validators.required])
  });

  get name() { return this.itemForm.get('name'); }
  get description() { return this.itemForm.get('description'); }
  get categories() { return this.itemForm.get('categories'); }

  submit() {
    if (this.itemForm.invalid) return;

    const formValue = this.itemForm.value;

    const payload = {
      name: formValue.name?.trim(),
      description: formValue.description?.trim(),
      categories: [formValue.categories?.trim()]
    };

    console.log('Submitting payload:', payload);

    this.adminService.createItem(payload).subscribe({
      next: () => {
        alert('Item added successfully!');
        this.itemCreated.emit();
      },
      error: (err) => {
        console.error('Failed to add item:', err);
        alert('Failed to add item:\n' + (err?.error?.title ?? 'Bad Request'));
      }
    });
  }

  cancelForm() {
    const confirmCancel = confirm('Are you sure you want to cancel?');
    if (confirmCancel) {
      this.cancel.emit();
    }
  }
}
