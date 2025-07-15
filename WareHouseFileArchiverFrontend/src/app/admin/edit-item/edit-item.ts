import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AdminService } from '../../services/admin.service';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-edit-item',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './edit-item.html',
  styleUrl: './edit-item.css'
})
export class EditItemComponent {
  @Input() item: any;
  @Output() cancel = new EventEmitter<void>();
  @Output() itemUpdated = new EventEmitter<void>();

  form: FormGroup;

  constructor(private fb: FormBuilder, private adminService: AdminService) {
    this.form = this.fb.group({
      name: ['', Validators.required],
      description: [''],
      categories: ['', Validators.required] // comma-separated
    });
  }

  ngOnInit() {
    if (this.item) {
      this.form.patchValue({
        name: this.item.name,
        description: this.item.description,
        categories: this.item.categories?.join(', ')
      });
    }
  }

  onSubmit() {
    if (!this.item?.id || this.form.invalid) return;

    const payload = {
      name: this.form.value.name,
      description: this.form.value.description,
      categories: this.form.value.categories
        .split(',')
        .map((cat: string) => cat.trim())
        .filter((cat: string) => !!cat)
    };

    this.adminService.updateItem(this.item.id, payload).subscribe({
      next: () => {
        alert('Item updated successfully!');
        this.itemUpdated.emit();
      },
      error: () => alert('Failed to update item.')
    });
  }
}
