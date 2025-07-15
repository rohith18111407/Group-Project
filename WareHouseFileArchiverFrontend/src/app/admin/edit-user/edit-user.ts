import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-edit-user',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './edit-user.html',
  styleUrl: './edit-user.css'
})
export class EditUserComponent {
  @Input() user: any;
  @Output() cancel = new EventEmitter<void>();
  @Output() userUpdated = new EventEmitter<void>();

  form: FormGroup;

  constructor(private fb: FormBuilder, private adminService: AdminService) {
    this.form = this.fb.group({
      username: ['', [Validators.required, Validators.email]],
      roles: [[], [Validators.required]]
    });
  }

  ngOnInit() {
    if (this.user) {
      this.form.patchValue({
        username: this.user.username,
        roles: this.user.roles
      });
    }
  }

  onSubmit() {
    if (this.form.invalid || !this.user?.id) return;

    const payload = {
      username: this.form.value.username,
      roles: this.form.value.roles
    };

    this.adminService.updateUser(this.user.id, payload).subscribe({
      next: () => {
        alert('User updated successfully!');
        this.userUpdated.emit();
      },
      error: () => alert('Failed to update user.')
    });
  }
}
