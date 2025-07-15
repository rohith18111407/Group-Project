import { Component, EventEmitter, inject, Output } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminService } from '../../services/admin.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-add-user',
  imports: [FormsModule, ReactiveFormsModule, CommonModule],
  templateUrl: './add-user.html',
  styleUrl: './add-user.css'
})
export class AddUserComponent {
  @Output() cancel = new EventEmitter<void>();
  @Output() userCreated = new EventEmitter<void>();
  adminService = inject(AdminService);

  userForm = new FormGroup({
    username: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required, Validators.minLength(6)]),
    role: new FormControl('Admin', Validators.required)
  });



  get username() { return this.userForm.get('username'); }
  get password() { return this.userForm.get('password'); }
  get role() { return this.userForm.get('role'); }

  submit() {
    if (this.userForm.invalid) return;

    const { username, password, role } = this.userForm.value;

    const payload = {
      username: username ?? '',
      password: password ?? '',
      roles: [role ?? '']
    };

    this.adminService.createUser(payload).subscribe({
      next: () => {
        alert('User added successfully!');
        this.userCreated.emit();
      },
      error: () => alert('Failed to add user.')
    });
  }


  cancelForm() {
    if (confirm('Are you sure you want to cancel?')) {
      this.cancel.emit();
    }
  }
}
